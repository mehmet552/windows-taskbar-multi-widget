using System;
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
using Windows.Media.Control;

namespace TaskbarMusicWidget.Widgets
{
    public partial class MusicDrawer : Window
    {
        // ── Win32 ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr insert, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] static extern int  SetWindowLong(IntPtr hWnd, int idx, int value);
        [DllImport("user32.dll")] static extern int  GetWindowLong(IntPtr hWnd, int idx);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        private static readonly IntPtr HWND_TOPMOST  = new(-1);
        private const uint SWP_SHOWWINDOW  = 0x0040;
        private const uint SWP_NOACTIVATE  = 0x0010;
        private const int  GWL_EXSTYLE     = -20;
        private const int  WS_EX_TOOLWINDOW = 0x00000080;
        private const int  WS_EX_NOACTIVATE = 0x08000000;

        // ── State ──────────────────────────────────────────────────────────────
        private GlobalSystemMediaTransportControlsSession? _session;
        private bool _isDraggingTimeline;
        private bool _isMuted;
        private float _volumeBeforeMute = 50f;
        private bool _isUpdatingVolume;
        private bool _isPlaying;

        private DispatcherTimer? _timelineTimer;
        private DispatcherTimer? _autoHideTimer;

        // Called by MusicWidget so we can invoke media commands
        public event Func<Task>? PlayPauseRequested;
        public event Func<Task>? PrevRequested;
        public event Func<Task>? NextRequested;
        public event Func<long, Task>? SeekRequested;
        public event Action<float>? VolumeChangeRequested;

        // ── Constructor ────────────────────────────────────────────────────────
        public MusicDrawer()
        {
            InitializeComponent();
            SourceInitialized += OnSourceInit;
            MouseLeave        += OnMouseLeave;
        }

        private void OnSourceInit(object? sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            int ex = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            // Start the timeline refresh timer
            _timelineTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timelineTimer.Tick += (_, _) => RefreshTimeline();
            _timelineTimer.Start();

            // Auto-hide if mouse not over drawer for 2s
            _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _autoHideTimer.Tick += (_, _) => { _autoHideTimer.Stop(); SlideOut(); };
            _autoHideTimer.Start();
        }

        // ── Public API (called by MusicWidget) ────────────────────────────────

        /// <summary>
        /// Position and size the drawer window directly above the parent widget window.
        /// </summary>
        public void PositionAbove(IntPtr parentHwnd, double dpi, int drawerHeight)
        {
            GetWindowRect(parentHwnd, out RECT pr);
            int physH = drawerHeight > 0 ? (int)(drawerHeight * dpi) : (int)(145 * dpi);
            int x = pr.Left;
            int y = pr.Top - physH;
            int w = pr.Right - pr.Left;
            SetWindowPos(new WindowInteropHelper(this).Handle,
                         HWND_TOPMOST, x, y, w, physH,
                         SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }

        public void SetAlbumArt(BitmapSource? bmp)
        {
            if (bmp == null)
            {
                AlbumBg.Source = null;
                return;
            }
            try 
            {
                // BlurEffect'in software rendering'de patlamaması (ve hızlı çalışması) için resmi küçültüyoruz
                var scaled = new TransformedBitmap(bmp, 
                    new ScaleTransform(150.0 / bmp.PixelWidth, 150.0 / bmp.PixelHeight));
                AlbumBg.Source = scaled;
            }
            catch 
            {
                AlbumBg.Source = bmp;
            }
        }

        public void SetTrackInfo(string title, string artist)
        {
            TxtTitle.Text  = title;
            TxtArtist.Text = artist;
        }

        public void SetPlayState(bool isPlaying)
        {
            _isPlaying = isPlaying;
            BtnPlay.Content = isPlaying ? "\u23F8" : "\u25B6";
        }

        public void SetSession(GlobalSystemMediaTransportControlsSession? session)
        {
            _session = session;
            RefreshTimeline();
        }

        public void SetAccentColor(Color accent)
        {
            Resources["DrawerAccentBrush"] = new SolidColorBrush(
                Color.FromArgb(0xDD, accent.R, accent.G, accent.B));
        }

        public void SetTintColor(Color dominant)
        {
            TintBrush.Color = Color.FromArgb(0x55, dominant.R, dominant.G, dominant.B);
        }

        public void SetVolume(float vol)
        {
            _isUpdatingVolume = true;
            SldVolume.Value  = vol;
            TxtVolVal.Text   = ((int)vol).ToString();
            _isUpdatingVolume = false;
        }

        private int _drawerHeight = 145;
        private bool _isClosing = false;

        // ── Slide-in animation ─────────────────────────────────────────────────
        public void SlideIn(int drawerHeight)
        {
            _drawerHeight = drawerHeight;
            SlideTransform.Y = drawerHeight;
            Opacity          = 0;

            var slideAnim = new DoubleAnimation(drawerHeight, 0,
                new Duration(TimeSpan.FromMilliseconds(240)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            SlideTransform.BeginAnimation(TranslateTransform.YProperty, slideAnim);

            var fadeAnim = new DoubleAnimation(0, 1,
                new Duration(TimeSpan.FromMilliseconds(220)));
            BeginAnimation(OpacityProperty, fadeAnim);
        }

        // ── Slide-out animation ────────────────────────────────────────────────
        public void SlideOut()
        {
            if (_isClosing) return;
            _isClosing = true;

            var slideAnim = new DoubleAnimation(0, _drawerHeight,
                new Duration(TimeSpan.FromMilliseconds(240)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            SlideTransform.BeginAnimation(TranslateTransform.YProperty, slideAnim);

            var fadeAnim = new DoubleAnimation(1, 0,
                new Duration(TimeSpan.FromMilliseconds(220)));
            fadeAnim.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fadeAnim);
        }

        // ── Timeline ──────────────────────────────────────────────────────────
        private void RefreshTimeline()
        {
            if (_isDraggingTimeline || _session == null) return;
            try
            {
                var props = _session.GetTimelineProperties();
                if (props != null && props.EndTime > TimeSpan.Zero)
                {
                    var pos = props.Position;
                    if (_isPlaying && props.LastUpdatedTime != default)
                    {
                        pos += (DateTimeOffset.Now - props.LastUpdatedTime);
                        if (pos > props.EndTime) pos = props.EndTime;
                    }

                    TxtDuration.Text  = props.EndTime.ToString(@"m\:ss");
                    TxtPosition.Text  = pos.ToString(@"m\:ss");
                    SldTimeline.Value = (pos.TotalSeconds / props.EndTime.TotalSeconds) * 100.0;
                }
            }
            catch { }
        }

        // ── Button handlers ────────────────────────────────────────────────────
        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (PlayPauseRequested != null) await PlayPauseRequested();
        }

        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (PrevRequested != null) await PrevRequested();
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (NextRequested != null) await NextRequested();
        }

        private async void BtnSeekBack_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;
            try
            {
                var props = _session.GetTimelineProperties();
                if (props != null && props.EndTime > TimeSpan.Zero)
                {
                    var newPos = props.Position - TimeSpan.FromSeconds(10);
                    if (newPos < TimeSpan.Zero) newPos = TimeSpan.Zero;
                    if (SeekRequested != null) await SeekRequested(newPos.Ticks);
                    RefreshTimeline();
                }
            }
            catch { }
        }

        private async void BtnSeekFwd_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;
            try
            {
                var props = _session.GetTimelineProperties();
                if (props != null && props.EndTime > TimeSpan.Zero)
                {
                    var newPos = props.Position + TimeSpan.FromSeconds(10);
                    if (newPos > props.EndTime) newPos = props.EndTime;
                    if (SeekRequested != null) await SeekRequested(newPos.Ticks);
                    RefreshTimeline();
                }
            }
            catch { }
        }

        private void BtnMute_Click(object sender, RoutedEventArgs e)
        {
            if (_isMuted)
            {
                _isMuted = false;
                VolumeChangeRequested?.Invoke(_volumeBeforeMute);
                SetVolume(_volumeBeforeMute);
                TxtMuteIcon.Text = "\uD83D\uDD0A";
            }
            else
            {
                _volumeBeforeMute = (float)SldVolume.Value;
                _isMuted = true;
                VolumeChangeRequested?.Invoke(0);
                SetVolume(0);
                TxtMuteIcon.Text = "\uD83D\uDD07";
            }
        }

        // ── Slider handlers ────────────────────────────────────────────────────
        private void SldTimeline_Down(object sender, MouseButtonEventArgs e) =>
            _isDraggingTimeline = true;

        private async void SldTimeline_Up(object sender, MouseButtonEventArgs e)
        {
            if (_session != null)
            {
                var props = _session.GetTimelineProperties();
                if (props != null && props.EndTime > TimeSpan.Zero)
                {
                    long ticks = (long)(props.EndTime.Ticks * (SldTimeline.Value / 100.0));
                    if (SeekRequested != null) await SeekRequested(ticks);
                }
            }
            _isDraggingTimeline = false;
        }

        private void SldTimeline_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingTimeline && _session != null)
            {
                var props = _session.GetTimelineProperties();
                if (props != null && props.EndTime > TimeSpan.Zero)
                    TxtPosition.Text = TimeSpan.FromTicks(
                        (long)(props.EndTime.Ticks * (e.NewValue / 100.0))).ToString(@"m\:ss");
            }
        }

        private void SldVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TxtVolVal.Text = ((int)e.NewValue).ToString();
            if (!_isUpdatingVolume)
                VolumeChangeRequested?.Invoke((float)e.NewValue);
        }

        // ── Auto-hide on mouse leave ───────────────────────────────────────────
        private void Content_MouseEnter(object sender, MouseEventArgs e) =>
            _autoHideTimer?.Stop();

        private void Content_MouseLeave(object sender, MouseEventArgs e) =>
            _autoHideTimer?.Start();

        private void OnMouseLeave(object sender, MouseEventArgs e) =>
            _autoHideTimer?.Start();

        protected override void OnClosed(EventArgs e)
        {
            _timelineTimer?.Stop();
            _autoHideTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
