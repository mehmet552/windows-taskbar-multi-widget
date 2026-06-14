using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TaskbarMusicWidget.Widgets
{
    public partial class StopwatchWidget : UserControl
    {
        private readonly Stopwatch _sw = new();
        private readonly DispatcherTimer _timer;
        private readonly List<TimeSpan> _laps = new();
        private bool _running;

        public StopwatchWidget()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (_, _) => UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            var e = _sw.Elapsed;
            if (e.TotalHours >= 1)
                TxtTime.Text = e.ToString(@"h\:mm\:ss\.f");
            else
                TxtTime.Text = e.ToString(@"mm\:ss\.f");
        }

        private void BtnStartPause_Click(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                _sw.Stop();
                _timer.Stop();
                _running = false;
                BtnStartPause.Content = "▶";
                TxtLabel.Text = "PAUSED";
            }
            else
            {
                _sw.Start();
                _timer.Start();
                _running = true;
                BtnStartPause.Content = "⏸";
                TxtLabel.Text = "RUNNING";
                BtnLap.IsEnabled = true;
            }
        }

        private void BtnLap_Click(object sender, RoutedEventArgs e)
        {
            if (!_running) return;
            var lap = _sw.Elapsed;
            _laps.Add(lap);
            TxtLabel.Text = $"LAP {_laps.Count}: {FormatTime(lap)}";
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _sw.Reset();
            _timer.Stop();
            _running = false;
            _laps.Clear();
            BtnStartPause.Content = "▶";
            BtnLap.IsEnabled = false;
            TxtLabel.Text = "STOPWATCH";
            TxtTime.Text = "00:00.0";
        }

        private static string FormatTime(TimeSpan t)
        {
            if (t.TotalHours >= 1) return t.ToString(@"h\:mm\:ss\.f");
            return t.ToString(@"mm\:ss\.f");
        }
    }
}
