using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace TaskbarMusicWidget.Widgets
{
    public partial class MusicWidget : UserControl
    {
        [DllImport("user32.dll")]
        static extern void keybd_event(byte vk, byte scan, uint flags, uint extra);

        private const byte VK_MEDIA_NEXT = 0xB0;
        private const byte VK_MEDIA_PREV = 0xB1;
        private const byte VK_MEDIA_PLAY = 0xB3;

        private GlobalSystemMediaTransportControlsSessionManager? _mgr;
        private GlobalSystemMediaTransportControlsSession? _session;
        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            private set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    IsPlayingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler? IsPlayingChanged;
        private bool _scrolling;
        private Storyboard? _scrollSb;

        private DispatcherTimer? _timelineTimer;
        private DispatcherTimer? _timelineHideTimer;
        private bool _isUpdatingVolume = true;
        private bool _isDraggingTimeline;

        // Stored to allow proper unsubscription — prevents GC from holding this widget alive
        private UserPreferenceChangedEventHandler? _themeHandler;

        public MusicWidget()
        {
            InitializeComponent();
            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;

            // Keep reference to handler so we can unsubscribe — static event holds strong reference!
            _themeHandler = (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General) ApplyTextTheme();
            };
            SystemEvents.UserPreferenceChanged += _themeHandler;
            ApplyTextTheme();
            BuildScrollStoryboard();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AudioManager.Initialize();
            AudioManager.VolumeChanged += OnVolumeChanged;

            _isUpdatingVolume = true;
            SldVolume.Value = AudioManager.GetMasterVolume();
            _isUpdatingVolume = false;

            // Timeline timer: only ticks when popup is actually open
            // Start/stop is managed by AlbumArt_Click and TimelinePopup_Closed
            _timelineTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timelineTimer.Tick += (_, _) => UpdateTimelineUI();
            // Note: NOT started here — started only when popup opens

            _timelineHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _timelineHideTimer.Tick += (_, _) =>
            {
                TimelinePopup.IsOpen = false;
                _timelineTimer?.Stop();       // ← Stop ticking when popup is hidden
                _timelineHideTimer!.Stop();
            };

            _ = InitSmtcAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _timelineTimer?.Stop();
            _timelineHideTimer?.Stop();
            _scrollSb?.Stop(this);
            AudioManager.VolumeChanged -= OnVolumeChanged;

            // Unsubscribe static event — without this the GC cannot collect this UserControl
            if (_themeHandler != null)
            {
                SystemEvents.UserPreferenceChanged -= _themeHandler;
                _themeHandler = null;
            }
        }

        private void OnVolumeChanged(float vol)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (Math.Abs(vol - SldVolume.Value) > 1.0)
                {
                    _isUpdatingVolume = true;
                    SldVolume.Value = vol;
                    _isUpdatingVolume = false;
                }
            });
        }

        // ── SMTC ──────────────────────────────────────────────────────────────
        private async Task InitSmtcAsync()
        {
            try
            {
                _mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                _mgr.CurrentSessionChanged += (s, _) =>
                    Dispatcher.Invoke(() => HookSession(s.GetCurrentSession()));
                HookSession(_mgr.GetCurrentSession());
            }
            catch { ShowIdle(); }
        }

        private void HookSession(GlobalSystemMediaTransportControlsSession? s)
        {
            if (_session != null)
            {
                _session.MediaPropertiesChanged -= OnMediaChanged;
                _session.PlaybackInfoChanged    -= OnPlaybackChanged;
            }
            _session = s;
            if (_session != null)
            {
                _session.MediaPropertiesChanged += OnMediaChanged;
                _session.PlaybackInfoChanged    += OnPlaybackChanged;
                _ = RefreshMediaAsync();
                RefreshPlayback();
            }
            else ShowIdle();
        }

        private void OnMediaChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e)
            => Dispatcher.Invoke(() => _ = RefreshMediaAsync());

        private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e)
            => Dispatcher.Invoke(RefreshPlayback);

        private async Task RefreshMediaAsync()
        {
            if (_session == null) { Dispatcher.Invoke(ShowIdle); return; }
            try
            {
                var props = await _session.TryGetMediaPropertiesAsync();
                if (props == null) { Dispatcher.Invoke(ShowIdle); return; }

                var title  = string.IsNullOrWhiteSpace(props.Title)  ? "Unknown" : props.Title;
                var artist = string.IsNullOrWhiteSpace(props.Artist) ? ""           : props.Artist;

                Dispatcher.Invoke(() =>
                {
                    TxtTitle.Text  = title;
                    TxtArtist.Text = artist;
                });

                if (props.Thumbnail != null)
                {
                    try
                    {
                        var stream = await props.Thumbnail.OpenReadAsync();
                        using var ms = new MemoryStream();
                        await stream.AsStreamForRead().CopyToAsync(ms);
                        ms.Position = 0;
                        var bmp = new BitmapImage();
                        Dispatcher.Invoke(() =>
                        {
                            bmp.BeginInit();
                            bmp.StreamSource  = ms;
                            bmp.CacheOption   = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            bmp.Freeze();
                            AlbumArt.Source            = bmp;
                            NoArtIcon.Visibility       = Visibility.Collapsed;
                        });
                    }
                    catch
                    {
                        Dispatcher.Invoke(() => { AlbumArt.Source = null; NoArtIcon.Visibility = Visibility.Visible; });
                    }
                }
                else
                {
                    Dispatcher.Invoke(() => { AlbumArt.Source = null; NoArtIcon.Visibility = Visibility.Visible; });
                }

                Dispatcher.Invoke(() =>
                {
                    TxtTitle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double w = TxtTitle.DesiredSize.Width;
                    bool needScroll = w > 105;
                    if (needScroll && !_scrolling)      { _scrolling = true;  _scrollSb?.Begin(this, true); }
                    else if (!needScroll && _scrolling) { _scrolling = false; _scrollSb?.Stop(this); TxtTitleTransform.X = 0; }
                });
            }
            catch { Dispatcher.Invoke(ShowIdle); }
        }

        private void RefreshPlayback()
        {
            if (_session == null) return;
            try
            {
                var info = _session.GetPlaybackInfo();
                IsPlaying = info?.PlaybackStatus ==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                BtnPlay.Content = IsPlaying ? "\u23F8" : "\u25B6";
            }
            catch { }
        }

        private void ShowIdle()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(ShowIdle); return; }
            try
            {
                TxtTitle.Text           = "Not playing";
                TxtArtist.Text          = "";
                AlbumArt.Source         = null;
                NoArtIcon.Visibility    = Visibility.Visible;
                BtnPlay.Content         = "\u25B6";
                IsPlaying               = false;
                _scrollSb?.Stop(this);
                _scrolling              = false;
                TxtTitleTransform.X     = 0;
            }
            catch { }
        }

        private void BuildScrollStoryboard()
        {
            _scrollSb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var anim = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(-140, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(6))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(-140, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(8))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(10))));
            Storyboard.SetTargetName(anim, "TxtTitleTransform");
            Storyboard.SetTargetProperty(anim, new PropertyPath(TranslateTransform.XProperty));
            _scrollSb.Children.Add(anim);
        }

        private void UpdateTimelineUI()
        {
            if (!TimelinePopup.IsOpen || _isDraggingTimeline || _session == null) return;
            try
            {
                var props = _session.GetTimelineProperties();
                if (props != null && props.EndTime > TimeSpan.Zero)
                {
                    TxtDuration.Text  = props.EndTime.ToString(@"m\:ss");
                    TxtPosition.Text  = props.Position.ToString(@"m\:ss");
                    SldTimeline.Value = (props.Position.TotalSeconds / props.EndTime.TotalSeconds) * 100.0;
                }
            }
            catch { }
        }

        // ── Button handlers ────────────────────────────────────────────────────
        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            keybd_event(VK_MEDIA_PREV, 0, 0, 0);
            keybd_event(VK_MEDIA_PREV, 0, 2, 0);
        }
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            keybd_event(VK_MEDIA_PLAY, 0, 0, 0);
            keybd_event(VK_MEDIA_PLAY, 0, 2, 0);
        }
        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            keybd_event(VK_MEDIA_NEXT, 0, 0, 0);
            keybd_event(VK_MEDIA_NEXT, 0, 2, 0);
        }

        private void AlbumArt_Click(object sender, MouseButtonEventArgs e)
        {
            if (_session != null)
            {
                TimelinePopup.IsOpen = true;
                _timelineTimer?.Start();   // ← Start timer only when popup opens
                UpdateTimelineUI();
                _timelineHideTimer?.Start();
            }
        }

        private void AlbumArtContainer_MouseEnter(object sender, MouseEventArgs e) =>
            _timelineHideTimer?.Stop();

        private void AlbumArtContainer_MouseLeave(object sender, MouseEventArgs e)
        {
            if (TimelinePopup.IsOpen) _timelineHideTimer?.Start();
        }

        private void TimelinePopup_MouseEnter(object sender, MouseEventArgs e) =>
            _timelineHideTimer?.Stop();

        private void TimelinePopup_MouseLeave(object sender, MouseEventArgs e)
        {
            if (TimelinePopup.IsOpen) _timelineHideTimer?.Start();
        }

        private void SldTimeline_MouseDown(object sender, MouseButtonEventArgs e) =>
            _isDraggingTimeline = true;

        private async void SldTimeline_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_session != null)
            {
                var props = _session.GetTimelineProperties();
                if (props != null && props.EndTime > TimeSpan.Zero)
                {
                    double percent    = SldTimeline.Value / 100.0;
                    long targetTicks  = (long)(props.EndTime.Ticks * percent);
                    await _session.TryChangePlaybackPositionAsync(targetTicks);
                }
            }
            _isDraggingTimeline = false;
        }

        private void SldTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingTimeline && _session != null)
            {
                var props = _session.GetTimelineProperties();
                if (props != null && props.EndTime > TimeSpan.Zero)
                {
                    TimeSpan pos    = TimeSpan.FromTicks((long)(props.EndTime.Ticks * (e.NewValue / 100.0)));
                    TxtPosition.Text = pos.ToString(@"m\:ss");
                }
            }
        }

        private void SldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtVolumeVal != null) TxtVolumeVal.Text = ((int)e.NewValue).ToString();
            if (!_isUpdatingVolume) AudioManager.SetMasterVolume((float)e.NewValue);
        }

        private void Volume_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            float vol  = AudioManager.GetMasterVolume();
            vol = e.Delta > 0 ? Math.Min(100f, vol + 2f) : Math.Max(0f, vol - 2f);
            AudioManager.SetMasterVolume(vol);
            _isUpdatingVolume = true;
            SldVolume.Value   = vol;
            _isUpdatingVolume = false;
        }

        private void Volume_MouseEnter(object sender, MouseEventArgs e) =>
            VolPopup.IsOpen = true;

        private void Volume_MouseLeave(object sender, MouseEventArgs e) =>
            Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(150);
                if (!VolPopup.IsMouseOver && !VolContainer.IsMouseOver) VolPopup.IsOpen = false;
            });

        private void VolPopup_MouseLeave(object sender, MouseEventArgs e) =>
            Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(150);
                if (!VolPopup.IsMouseOver && !VolContainer.IsMouseOver) VolPopup.IsOpen = false;
            });

        private void ApplyTextTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                bool isDark = true;
                if (key?.GetValue("AppsUseLightTheme") is int v) isDark = v == 0;

                if (TxtTitle != null)
                    TxtTitle.Foreground = isDark ? Brushes.White : Brushes.Black;
                if (TxtArtist != null)
                    TxtArtist.Foreground = new SolidColorBrush(
                        isDark ? Color.FromRgb(0x88, 0x88, 0x88) : Color.FromRgb(0x44, 0x44, 0x44));
            }
            catch { }
        }
    }
}
