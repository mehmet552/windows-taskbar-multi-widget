using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
        // ── Win32 (for DPI + parent HWND) ──────────────────────────────────────
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("shcore.dll")] static extern int    GetDpiForMonitor(IntPtr hmon, int type, out uint dpiX, out uint dpiY);

        // ── SMTC ───────────────────────────────────────────────────────────────
        private GlobalSystemMediaTransportControlsSessionManager? _mgr;
        private GlobalSystemMediaTransportControlsSession? _session;

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            private set
            {
                if (_isPlaying == value) return;
                _isPlaying = value;
                if (_isPlaying) _progressTimer?.Start();
                else _progressTimer?.Stop();
                IsPlayingChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler? IsPlayingChanged;

        // ── Scroll animation ───────────────────────────────────────────────────
        private bool _scrolling;
        private Storyboard? _scrollSb;

        // ── Album art + colours ────────────────────────────────────────────────
        private BitmapImage? _currentBmp;
        private Color _lastDominant = Color.FromRgb(0x11, 0x11, 0x11);
        private Color _lastAccent   = Color.FromRgb(0x6E, 0xE7, 0xF5);

        // ── Progress timer (for the top widget bar) ────────────────────────────
        private DispatcherTimer? _progressTimer;

        // ── Drawer window ──────────────────────────────────────────────────────
        private MusicDrawer? _drawer;
        private const int DrawerHeight = 175; // logical pixels (increased to show volume bar)

        // ── Volume ─────────────────────────────────────────────────────────────
        private bool _isUpdatingVolume;
        private UserPreferenceChangedEventHandler? _themeHandler;

        // ── Constructor ────────────────────────────────────────────────────────
        public MusicWidget()
        {
            InitializeComponent();
            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;

            _progressTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(500) };
            _progressTimer.Tick += OnProgressTick;

            _themeHandler = (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General) ApplyTextTheme();
            };
            SystemEvents.UserPreferenceChanged += _themeHandler;
            ApplyTextTheme();
            BuildScrollStoryboard();
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AudioManager.Initialize();
            AudioManager.VolumeChanged += OnVolumeChanged;

            _isUpdatingVolume = true;
            float vol = AudioManager.GetMasterVolume();
            TxtVolPercent.Text = ((int)vol).ToString();
            _isUpdatingVolume = false;

            _ = InitSmtcAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _scrollSb?.Stop(this);
            AudioManager.VolumeChanged -= OnVolumeChanged;
            CloseDrawer();

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
                TxtVolPercent.Text = ((int)vol).ToString();
                _drawer?.SetVolume(vol);

                double ratio = vol / 100.0;
                if (Window.GetWindow(this) is MainWindow mw)
                    mw.SetVolumeScale(Math.Max(0, Math.Min(1, ratio)));
            });
        }

        // ── SMTC ───────────────────────────────────────────────────────────────
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

                string title  = string.IsNullOrWhiteSpace(props.Title)  ? "Unknown" : props.Title;
                string artist = string.IsNullOrWhiteSpace(props.Artist) ? ""        : props.Artist;

                BitmapImage? bmp = null;
                if (props.Thumbnail != null)
                {
                    try
                    {
                        var stream = await props.Thumbnail.OpenReadAsync();
                        using var ms = new MemoryStream();
                        await stream.AsStreamForRead().CopyToAsync(ms);
                        ms.Position = 0;
                        bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = ms;
                        bmp.CacheOption  = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                    }
                    catch { bmp = null; }
                }

                // Extract colours on background thread
                Color dominant = Color.FromRgb(0x11, 0x11, 0x11);
                Color accent   = Color.FromRgb(0x6E, 0xE7, 0xF5);
                if (bmp != null) ExtractColors(bmp, out dominant, out accent);

                Dispatcher.Invoke(() =>
                {
                    _currentBmp    = bmp;
                    _lastDominant  = dominant;
                    _lastAccent    = accent;

                    if (Window.GetWindow(this) is MainWindow mw)
                        mw.SetTimelineColor(accent);

                    TxtTitle.Text  = title;
                    TxtArtist.Text = artist;

                    AlbumArt.Source         = bmp;
                    NoArtIcon.Visibility    = bmp != null ? Visibility.Collapsed : Visibility.Visible;

                    // Sync open drawer
                    if (_drawer != null && _drawer.IsVisible)
                    {
                        _drawer.SetAlbumArt(bmp);
                        _drawer.SetTrackInfo(title, artist);
                        _drawer.SetAccentColor(accent);
                        _drawer.SetTintColor(dominant);
                    }

                    // Scrolling title
                    TxtTitle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    bool needScroll = TxtTitle.DesiredSize.Width > 105;
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
                _drawer?.SetPlayState(IsPlaying);
                UpdateTimelineNow();
            }
            catch { }
        }

        private void OnProgressTick(object? sender, EventArgs e) => UpdateTimelineNow();

        private void UpdateTimelineNow()
        {
            if (_session == null) return;
            try
            {
                var props = _session.GetTimelineProperties();
                if (props != null && props.EndTime > TimeSpan.Zero)
                {
                    var pos = props.Position;
                    if (IsPlaying && props.LastUpdatedTime != default)
                    {
                        pos += (DateTimeOffset.Now - props.LastUpdatedTime);
                        if (pos > props.EndTime) pos = props.EndTime;
                    }

                    double ratio = pos.TotalSeconds / props.EndTime.TotalSeconds;
                    if (Window.GetWindow(this) is MainWindow mw)
                        mw.SetTimelineScale(Math.Max(0, Math.Min(1, ratio)));
                }
            }
            catch { }
        }

        private void ShowIdle()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(ShowIdle); return; }
            try
            {
                _currentBmp             = null;
                TxtTitle.Text           = "Not playing";
                TxtArtist.Text          = "";
                AlbumArt.Source         = null;
                NoArtIcon.Visibility    = Visibility.Visible;
                BtnPlay.Content         = "\u25B6";
                IsPlaying               = false;
                _scrollSb?.Stop(this);
                _scrolling              = false;
                TxtTitleTransform.X     = 0;
                _drawer?.SetAlbumArt(null);
                _drawer?.SetTrackInfo("Not playing", "");
            }
            catch { }
        }

        // ── Scroll storyboard ─────────────────────────────────────────────────
        private void BuildScrollStoryboard()
        {
            _scrollSb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var anim  = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(-140, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(6))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(-140, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(8))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0,    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(10))));
            Storyboard.SetTargetName(anim, "TxtTitleTransform");
            Storyboard.SetTargetProperty(anim, new PropertyPath(TranslateTransform.XProperty));
            _scrollSb.Children.Add(anim);
        }

        // ── Drawer management ─────────────────────────────────────────────────
        private void ToggleDrawer()
        {
            if (_drawer != null && _drawer.IsVisible)
            {
                CloseDrawer();
            }
            else
            {
                OpenDrawer();
            }
        }

        private void OpenDrawer()
        {
            CloseDrawer(); // ensure clean state

            var parentWin = Window.GetWindow(this);
            if (parentWin == null) return;

            // Get parent HWND and DPI for Win32 positioning
            var helper     = new WindowInteropHelper(parentWin);
            IntPtr hwnd    = helper.Handle;
            double dpi     = GetDpi(hwnd);

            _drawer = new MusicDrawer();

            // Wire up events so drawer can call SMTC methods back
            _drawer.PlayPauseRequested   += async () => await PlayPauseAsync();
            _drawer.PrevRequested        += async () => await PrevAsync();
            _drawer.NextRequested        += async () => await NextAsync();
            _drawer.SeekRequested        += async ticks => await SeekAsync(ticks);
            _drawer.VolumeChangeRequested += vol =>
            {
                AudioManager.SetMasterVolume(vol);
                TxtVolPercent.Text = ((int)vol).ToString();
            };
            _drawer.Closed += (_, _) => _drawer = null;

            // Show before positioning (so Handle is available)
            _drawer.Show();
            _drawer.PositionAbove(hwnd, dpi, DrawerHeight);

            // Populate drawer
            _drawer.SetAlbumArt(_currentBmp);
            _drawer.SetTrackInfo(TxtTitle.Text, TxtArtist.Text);
            _drawer.SetPlayState(IsPlaying);
            _drawer.SetSession(_session);
            _drawer.SetAccentColor(_lastAccent);
            _drawer.SetTintColor(_lastDominant);
            _drawer.SetVolume(AudioManager.GetMasterVolume());

            _drawer.SlideIn(DrawerHeight);
        }

        private void CloseDrawer()
        {
            if (_drawer != null)
            {
                try { _drawer.Close(); } catch { }
                _drawer = null;
            }
        }

        private static double GetDpi(IntPtr hwnd)
        {
            try
            {
                IntPtr hmon = MonitorFromWindow(hwnd, 2);
                GetDpiForMonitor(hmon, 0, out uint dpiX, out _);
                return dpiX / 96.0;
            }
            catch { return 1.25; }
        }

        // ── Widget button handlers ─────────────────────────────────────────────
        private void AlbumArt_Click(object sender, MouseButtonEventArgs e) => ToggleDrawer();
        private void Volume_Click(object sender, MouseButtonEventArgs e)   => ToggleDrawer();

        private async void BtnPrev_Click(object sender, RoutedEventArgs e) => await PrevAsync();
        private async void BtnPlay_Click(object sender, RoutedEventArgs e) => await PlayPauseAsync();
        private async void BtnNext_Click(object sender, RoutedEventArgs e) => await NextAsync();

        // ── SMTC command helpers ───────────────────────────────────────────────
        private async Task PlayPauseAsync()
        {
            if (_session == null) return;
            try
            {
                if (IsPlaying) await _session.TryPauseAsync();
                else           await _session.TryPlayAsync();
            }
            catch { }
        }

        private async Task PrevAsync()
        {
            if (_session != null) try { await _session.TrySkipPreviousAsync(); } catch { }
        }

        private async Task NextAsync()
        {
            if (_session != null) try { await _session.TrySkipNextAsync(); } catch { }
        }

        private async Task SeekAsync(long ticks)
        {
            if (_session != null) try { await _session.TryChangePlaybackPositionAsync(ticks); } catch { }
        }

        // ── Volume scroll ─────────────────────────────────────────────────────
        private void Volume_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            float vol = AudioManager.GetMasterVolume();
            vol = e.Delta > 0 ? Math.Min(100f, vol + 2f) : Math.Max(0f, vol - 2f);
            AudioManager.SetMasterVolume(vol);
            TxtVolPercent.Text = ((int)vol).ToString();
            _drawer?.SetVolume(vol);
        }

        // ── Theme ─────────────────────────────────────────────────────────────
        private void ApplyTextTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                bool isDark = true;
                if (key?.GetValue("AppsUseLightTheme") is int v) isDark = v == 0;
                if (TxtTitle  != null) TxtTitle.Foreground = isDark ? Brushes.White : Brushes.Black;
                if (TxtArtist != null) TxtArtist.Foreground = new SolidColorBrush(
                    isDark ? Color.FromRgb(0x88, 0x88, 0x88) : Color.FromRgb(0x44, 0x44, 0x44));
            }
            catch { }
        }

        // ── Colour extraction from album art ──────────────────────────────────
        private static void ExtractColors(BitmapImage bmp, out Color dominant, out Color accent)
        {
            dominant = Color.FromRgb(0x11, 0x11, 0x11);
            accent   = Color.FromRgb(0x6E, 0xE7, 0xF5);
            try
            {
                const int size = 20;
                var scaled = new TransformedBitmap(bmp,
                    new ScaleTransform(size / (double)bmp.PixelWidth,
                                       size / (double)bmp.PixelHeight));
                int stride = scaled.PixelWidth * 4;
                var px     = new byte[scaled.PixelHeight * stride];
                scaled.CopyPixels(px, stride, 0);

                long sumR = 0, sumG = 0, sumB = 0, count = 0;
                double bestScore = -1, bestR = 0x6E, bestG = 0xE7, bestB = 0xF5;

                for (int i = 0; i < px.Length; i += 4)
                {
                    if (px[i + 3] < 120) continue;
                    double b = px[i] / 255.0, g = px[i+1] / 255.0, r = px[i+2] / 255.0;
                    sumR += px[i+2]; sumG += px[i+1]; sumB += px[i]; count++;

                    double max = Math.Max(r, Math.Max(g, b));
                    double min = Math.Min(r, Math.Min(g, b));
                    double L   = (max + min) / 2.0;
                    double S   = (max - min < 1e-9) ? 0 : (max - min) / (1.0 - Math.Abs(2*L - 1));
                    double ls  = Math.Max(0, 1.0 - Math.Abs(L - 0.55) * 2.5);
                    double sc  = S * ls;
                    if (sc > bestScore) { bestScore = sc; bestR = r*255; bestG = g*255; bestB = b*255; }
                }

                if (count == 0) return;
                dominant = Color.FromRgb((byte)(sumR/count * 0.18),
                                         (byte)(sumG/count * 0.18),
                                         (byte)(sumB/count * 0.18));
                if (bestScore > 0.15)
                    accent = Color.FromRgb((byte)Math.Min(255, bestR * 1.25),
                                           (byte)Math.Min(255, bestG * 1.25),
                                           (byte)Math.Min(255, bestB * 1.25));
            }
            catch { }
        }
    }
}
