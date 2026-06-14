using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TaskbarMusicWidget.Services;
using TaskbarMusicWidget.Widgets;

namespace TaskbarMusicWidget
{
    public partial class MainWindow : Window
    {
        // ── Win32 API ──────────────────────────────────────────────────────────
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string? wnd);
        [DllImport("user32.dll")] static extern bool   SetWindowPos(IntPtr hWnd, IntPtr insert, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern bool   GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] static extern int    SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] static extern int    GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor, rcWork;
            public uint dwFlags;
        }

        private static readonly IntPtr HWND_TOPMOST   = new(-1);
        private const uint SWP_SHOWWINDOW  = 0x0040;
        private const uint SWP_NOACTIVATE  = 0x0010;
        private const uint SWP_NOMOVE      = 0x0002;
        private const uint SWP_NOSIZE      = 0x0001;
        private const int  GWL_EXSTYLE     = -20;
        private const int  WS_EX_TOOLWINDOW = 0x00000080;
        private const int  WS_EX_NOACTIVATE = 0x08000000;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        // MONITORINFO.cbSize is constant — compute once
        private static readonly int _monitorInfoSize = Marshal.SizeOf<MONITORINFO>();

        // ── Widget instances ───────────────────────────────────────────────────
        private readonly Dictionary<string, UserControl> _widgets = new();
        private DispatcherTimer _topmostTimer = null!;
        private DispatcherTimer _visualizerTimer = null!;
        private readonly Random _rand = new();

        // ── Position caching — avoids redundant SetWindowPos/DWM calls ─────────
        private IntPtr _cachedHWnd;
        private int _lastX = int.MinValue, _lastY = int.MinValue;
        private int _lastW = int.MinValue, _lastH = int.MinValue;
        private bool _lastFullScreen = false;

        // ── Theme event handler stored to allow proper unsubscription ──────────
        private UserPreferenceChangedEventHandler? _themeHandler;

        public MainWindow()
        {
            InitializeComponent();
            Loaded  += OnLoaded;
            Closed  += OnClosed;

            _visualizerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _visualizerTimer.Tick += (_, _) =>
            {
                VisBar1.Height = _rand.Next(2, 16);
                VisBar2.Height = _rand.Next(2, 16);
                VisBar3.Height = _rand.Next(2, 16);
            };

            // Store handler reference so we can unsubscribe on close (prevents memory leak)
            _themeHandler = (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General) ApplyTheme();
            };
            SystemEvents.UserPreferenceChanged += _themeHandler;
            ApplyTheme();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _cachedHWnd = new WindowInteropHelper(this).Handle;
            int ex = GetWindowLong(_cachedHWnd, GWL_EXSTYLE);
            SetWindowLong(_cachedHWnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            BuildWidgets();
            ShowCurrentWidget();

            // Position immediately so widget appears on taskbar at once (not after 1s delay)
            PositionOnTaskbar();

            // Then start the periodic re-assertion timer
            _topmostTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _topmostTimer.Tick += (_, _) => PositionOnTaskbar();
            _topmostTimer.Start();

            WidgetManager.Instance.ConfigChanged += OnConfigChanged;
            WidgetManager.Instance.WidgetChanged  += OnWidgetChanged;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _topmostTimer?.Stop();
            _visualizerTimer?.Stop();
            if (_themeHandler != null)
            {
                SystemEvents.UserPreferenceChanged -= _themeHandler;
                _themeHandler = null;
            }
            WidgetManager.Instance.ConfigChanged -= OnConfigChanged;
            WidgetManager.Instance.WidgetChanged  -= OnWidgetChanged;
            AudioManager.Cleanup();
        }

        // Separate named methods so they can be unsubscribed
        private void OnConfigChanged() =>
            Dispatcher.Invoke(() => { RebuildWidgets(); ShowCurrentWidget(); });

        private void OnWidgetChanged() =>
            Dispatcher.Invoke(ShowCurrentWidget);

        // ── Widget management ──────────────────────────────────────────────────
        private void BuildWidgets()
        {
            _widgets.Clear();
            foreach (var id in WidgetManager.Instance.GetActiveWidgets())
            {
                var w = CreateWidget(id);
                if (id == "Music" && w is MusicWidget music)
                    music.IsPlayingChanged += OnMusicPlayingChanged;
                _widgets[id] = w;
            }
            UpdateArrows();
            UpdateVisualizerState();
        }

        private void RebuildWidgets()
        {
            var active = WidgetManager.Instance.GetActiveWidgets();

            var toRemove = new List<string>();
            foreach (var kv in _widgets)
            {
                if (!active.Contains(kv.Key))
                {
                    if (kv.Key == "Music" && kv.Value is MusicWidget musicToRemove)
                        musicToRemove.IsPlayingChanged -= OnMusicPlayingChanged;
                    toRemove.Add(kv.Key);
                }
            }
            foreach (var k in toRemove) _widgets.Remove(k);

            foreach (var id in active)
            {
                if (!_widgets.ContainsKey(id))
                {
                    var w = CreateWidget(id);
                    if (id == "Music" && w is MusicWidget musicToAdd)
                        musicToAdd.IsPlayingChanged += OnMusicPlayingChanged;
                    _widgets[id] = w;
                }
            }

            UpdateArrows();
            UpdateVisualizerState();
        }

        private void OnMusicPlayingChanged(object? sender, EventArgs e) => Dispatcher.Invoke(UpdateVisualizerState);

        private void UpdateVisualizerState()
        {
            if (_widgets.TryGetValue("Music", out var w) && w is MusicWidget music)
            {
                VisualizerContainer.Visibility = Visibility.Visible;
                if (music.IsPlaying)
                {
                    _visualizerTimer.Start();
                }
                else
                {
                    _visualizerTimer.Stop();
                    VisBar1.Height = 2;
                    VisBar2.Height = 2;
                    VisBar3.Height = 2;
                }
            }
            else
            {
                VisualizerContainer.Visibility = Visibility.Collapsed;
                _visualizerTimer.Stop();
            }
        }

        private static UserControl CreateWidget(string id) => id switch
        {
            "Music"     => new MusicWidget(),
            "Timer"     => new TimerWidget(),
            "Stopwatch" => new StopwatchWidget(),
            "Pomodoro"  => new PomodoroWidget(),
            _           => throw new ArgumentException($"Unknown widget: {id}")
        };

        private void ShowCurrentWidget()
        {
            var id = WidgetManager.Instance.CurrentWidgetId;
            if (id != null && _widgets.TryGetValue(id, out var widget))
            {
                WidgetHost.Content = widget;
            }
            else if (_widgets.Count > 0)
            {
                using var enumerator = _widgets.Values.GetEnumerator();
                enumerator.MoveNext();
                WidgetHost.Content = enumerator.Current;
            }
            else
            {
                WidgetHost.Content = null;
            }
            UpdateArrows();
        }

        private void UpdateArrows()
        {
            bool canNav = WidgetManager.Instance.CanNavigate;
            BtnPrevWidget.Visibility = canNav ? Visibility.Visible : Visibility.Collapsed;
            BtnNextWidget.Visibility = canNav ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ReloadPomodoro()
        {
            if (_widgets.TryGetValue("Pomodoro", out var w) && w is PomodoroWidget pomo)
                pomo.ReloadSettings();
        }

        // ── Navigation ─────────────────────────────────────────────────────────
        private void BtnPrevWidget_Click(object sender, RoutedEventArgs e) =>
            WidgetManager.Instance.Previous();

        private void BtnNextWidget_Click(object sender, RoutedEventArgs e) =>
            WidgetManager.Instance.Next();

        // ── Taskbar positioning — optimized ────────────────────────────────────
        private void PositionOnTaskbar()
        {
            bool isFullScreen = IsForegroundFullScreen();

            // Only change visibility when fullscreen state actually changes
            if (isFullScreen != _lastFullScreen)
            {
                _lastFullScreen = isFullScreen;
                Visibility = isFullScreen ? Visibility.Hidden : Visibility.Visible;
            }
            if (isFullScreen) return;

            var taskbar = FindWindow("Shell_TrayWnd", null);
            var source  = PresentationSource.FromVisual(this);
            double dpi  = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.25;

            if (taskbar != IntPtr.Zero && GetWindowRect(taskbar, out RECT tb))
            {
                int tbH = tb.Bottom - tb.Top;
                int wH  = (int)(Height * dpi);
                int wW  = (int)(Width  * dpi);
                int tX  = tb.Left + 8;
                int tY  = tb.Top  + (tbH - wH) / 2;

                if (tX != _lastX || tY != _lastY || wW != _lastW || wH != _lastH)
                {
                    // Position or size changed — full repositioning call
                    _lastX = tX; _lastY = tY; _lastW = wW; _lastH = wH;
                    SetWindowPos(_cachedHWnd, HWND_TOPMOST, tX, tY, wW, wH, SWP_SHOWWINDOW | SWP_NOACTIVATE);
                }
                else
                {
                    // Position unchanged — cheap TOPMOST re-assertion only (no DWM repaint)
                    SetWindowPos(_cachedHWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                }
            }
            else
            {
                // Fallback: no taskbar found
                if (_lastX == int.MinValue)
                {
                    Left    = 4;
                    Top     = SystemParameters.PrimaryScreenHeight - Height - 4;
                    Topmost = true;
                    _lastX  = 0; // mark as initialized
                }
            }
        }

        private bool IsForegroundFullScreen()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            if (fg == GetDesktopWindow() || fg == GetShellWindow()) return false;

            GetWindowRect(fg, out RECT app);
            var hm = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
            // Use cached size constant instead of computing every call
            var mi = new MONITORINFO { cbSize = _monitorInfoSize };
            if (!GetMonitorInfo(hm, ref mi)) return false;

            return app.Left   <= mi.rcMonitor.Left  && app.Top    <= mi.rcMonitor.Top &&
                   app.Right  >= mi.rcMonitor.Right  && app.Bottom >= mi.rcMonitor.Bottom;
        }

        // ── Theme ──────────────────────────────────────────────────────────────
        private void ApplyTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                bool isDark = true;
                if (key?.GetValue("AppsUseLightTheme") is int v) isDark = v == 0;

                MainBorder.Background = new SolidColorBrush(isDark
                    ? Color.FromRgb(0x1A, 0x1A, 0x1A)
                    : Color.FromRgb(0xF2, 0xF2, 0xF2));
            }
            catch { }
        }
    }
}