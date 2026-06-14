using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TaskbarMusicWidget.Widgets
{
    public partial class TimerWidget : UserControl
    {
        private DispatcherTimer _timer;
        private TimeSpan _remaining;
        private TimeSpan _preset = TimeSpan.FromMinutes(5);
        private bool _running;
        private bool _finished;

        public TimerWidget()
        {
            InitializeComponent();
            _remaining = _preset;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTick;
            UpdateDisplay();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (_remaining <= TimeSpan.Zero)
            {
                _timer.Stop();
                _running = false;
                _finished = true;
                BtnStartPause.Content = "▶";
                TxtLabel.Text = "DONE!";
                TxtLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                ((App)Application.Current).ShowBalloonTip("Timer Finished!", $"Time is up. ({FormatTime(_preset)})");
                return;
            }
            _remaining -= TimeSpan.FromSeconds(1);
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            TxtTime.Text = FormatTime(_remaining);
        }

        private static string FormatTime(TimeSpan t)
        {
            if (t.TotalHours >= 1)
                return t.ToString(@"h\:mm\:ss");
            return t.ToString(@"mm\:ss");
        }

        private void BtnStartPause_Click(object sender, RoutedEventArgs e)
        {
            if (_finished)
            {
                // Reset and start fresh
                _remaining = _preset;
                _finished = false;
                TxtLabel.Text = "TIMER";
                TxtLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                UpdateDisplay();
            }

            if (_running)
            {
                _timer.Stop();
                _running = false;
                BtnStartPause.Content = "▶";
                TxtLabel.Text = "PAUSED";
            }
            else
            {
                _timer.Start();
                _running = true;
                BtnStartPause.Content = "⏸";
                TxtLabel.Text = "RUNNING";
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _running = false;
            _finished = false;
            _remaining = _preset;
            BtnStartPause.Content = "▶";
            TxtLabel.Text = "TIMER";
            TxtLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            UpdateDisplay();
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string tag && TimeSpan.TryParse(tag, out var ts))
            {
                SetPreset(ts);
            }
        }

        private void CustomTime_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Windows.CustomTimeDialog() { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true && dlg.SelectedTime.HasValue)
            {
                SetPreset(dlg.SelectedTime.Value);
            }
        }

        private void SetPreset(TimeSpan ts)
        {
            _timer.Stop();
            _running = false;
            _finished = false;
            _preset = ts;
            _remaining = ts;
            BtnStartPause.Content = "▶";
            TxtLabel.Text = "TIMER";
            TxtLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            UpdateDisplay();
        }
    }
}
