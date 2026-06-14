using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Input;
using TaskbarMusicWidget.Models;
using TaskbarMusicWidget.Services;

namespace TaskbarMusicWidget.Widgets
{
    public enum PomodoroPhase { Work, ShortBreak, LongBreak }

    public partial class PomodoroWidget : UserControl
    {
        private DispatcherTimer _timer;
        private TimeSpan _remaining;
        private bool _running;
        private PomodoroPhase _phase = PomodoroPhase.Work;
        private int _sessionCount = 0;

        // Colors per phase
        private static readonly Color WorkColor      = Color.FromRgb(0xEF, 0x44, 0x44);
        private static readonly Color ShortBreakColor = Color.FromRgb(0x22, 0xC5, 0x5E);
        private static readonly Color LongBreakColor  = Color.FromRgb(0x3B, 0x82, 0xF6);

        public PomodoroWidget()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTick;
            Loaded += (_, _) => ReloadSettings();
        }

        public void ReloadSettings()
        {
            if (_running) return; // don't interrupt active session
            LoadPhase(_phase);
        }

        private PomodoroSettings Settings => WidgetManager.Instance.Config.Pomodoro;

        private TimeSpan PhaseTime(PomodoroPhase p) => p switch
        {
            PomodoroPhase.Work       => TimeSpan.FromMinutes(Settings.WorkMinutes),
            PomodoroPhase.ShortBreak => TimeSpan.FromMinutes(Settings.ShortBreakMinutes),
            PomodoroPhase.LongBreak  => TimeSpan.FromMinutes(Settings.LongBreakMinutes),
            _ => TimeSpan.FromMinutes(25)
        };

        private void LoadPhase(PomodoroPhase phase)
        {
            _phase = phase;
            _remaining = PhaseTime(phase);
            UpdateDisplay();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (_remaining <= TimeSpan.Zero)
            {
                _timer.Stop();
                _running = false;
                BtnStartPause.Content = "▶";
                OnPhaseComplete();
                return;
            }
            _remaining -= TimeSpan.FromSeconds(1);
            UpdateDisplay();
        }

        private void OnPhaseComplete()
        {
            if (_phase == PomodoroPhase.Work)
            {
                _sessionCount++;
                string msg = $"🍅 Session {_sessionCount} completed!";
                bool longBreak = _sessionCount % Settings.SessionsBeforeLongBreak == 0;
                ((App)Application.Current).ShowBalloonTip("Pomodoro", longBreak
                    ? $"{msg} Time for a long break."
                    : $"{msg} Take a short break.");
                AdvancePhase();
            }
            else
            {
                ((App)Application.Current).ShowBalloonTip("Pomodoro", "Break is over! Time to work.");
                LoadPhase(PomodoroPhase.Work);
            }
        }

        private void AdvancePhase()
        {
            bool longBreak = _sessionCount % Settings.SessionsBeforeLongBreak == 0;
            LoadPhase(longBreak ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak);
        }

        private void UpdateDisplay()
        {
            TxtTime.Text = _remaining.ToString(@"mm\:ss");
            TxtSession.Text = (_sessionCount + 1).ToString();

            Color c = _phase switch
            {
                PomodoroPhase.ShortBreak => ShortBreakColor,
                PomodoroPhase.LongBreak  => LongBreakColor,
                _                        => WorkColor
            };

            string phaseLabel = _phase switch
            {
                PomodoroPhase.ShortBreak => "SHORT BREAK",
                PomodoroPhase.LongBreak  => "LONG BREAK",
                _                        => "WORK"
            };

            TxtPhase.Text = phaseLabel;
            TxtPhase.Foreground = new SolidColorBrush(c);
            ProgressArc.Stroke = new SolidColorBrush(c);

            // Update progress arc
            double total   = PhaseTime(_phase).TotalSeconds;
            double elapsed = total - _remaining.TotalSeconds;
            double progress = total > 0 ? elapsed / total : 0;
            DrawArc(ProgressArc, progress, 15.0, 15.0, 12.0);
        }

        private static void DrawArc(Path arc, double progress, double cx, double cy, double r)
        {
            if (progress <= 0) { arc.Data = null; return; }
            double p = Math.Min(progress, 0.9999);
            double startA = -Math.PI / 2;
            double endA   = startA + 2 * Math.PI * p;

            double x1 = cx + r * Math.Cos(startA);
            double y1 = cy + r * Math.Sin(startA);
            double x2 = cx + r * Math.Cos(endA);
            double y2 = cy + r * Math.Sin(endA);
            bool large = p > 0.5;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(x1, y1), false, false);
                ctx.ArcTo(new Point(x2, y2), new Size(r, r), 0, large,
                          SweepDirection.Clockwise, true, false);
            }
            geo.Freeze();
            arc.Data = geo;
        }

        private void BtnStartPause_Click(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                _timer.Stop();
                _running = false;
                BtnStartPause.Content = "▶";
            }
            else
            {
                _timer.Start();
                _running = true;
                BtnStartPause.Content = "⏸";
            }
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _running = false;
            BtnStartPause.Content = "▶";

            if (_phase == PomodoroPhase.Work)
            {
                _sessionCount++;
                AdvancePhase();
            }
            else
            {
                LoadPhase(PomodoroPhase.Work);
            }
        }

        private void Pomodoro_RightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var dlg = new Windows.PomodoroSettingsDialog() { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                ReloadSettings();
            }
        }
    }
}
