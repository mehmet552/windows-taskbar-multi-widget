using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TaskbarMusicWidget.Models;
using TaskbarMusicWidget.Services;

namespace TaskbarMusicWidget.Windows
{
    public partial class SetupWizard : Window
    {
        private int _page = 1;

        public SetupWizard()
        {
            InitializeComponent();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Border b && b.Tag is string id)
            {
                var chk = id switch
                {
                    "Music"     => ChkMusic,
                    "Timer"     => ChkTimer,
                    "Stopwatch" => ChkStopwatch,
                    "Pomodoro"  => ChkPomodoro,
                    _           => null
                };
                if (chk != null) chk.IsChecked = !chk.IsChecked;

                // Highlight selected card
                b.BorderBrush = (chk?.IsChecked == true)
                    ? new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF))
                    : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_page == 1)
            {
                // Save widget selections
                var mgr = WidgetManager.Instance;
                mgr.SetWidgetEnabled("Music",     ChkMusic.IsChecked     == true);
                mgr.SetWidgetEnabled("Timer",     ChkTimer.IsChecked     == true);
                mgr.SetWidgetEnabled("Stopwatch", ChkStopwatch.IsChecked == true);
                mgr.SetWidgetEnabled("Pomodoro",  ChkPomodoro.IsChecked  == true);

                // Mark first run as done
                mgr.Config.IsFirstRun = false;
                mgr.Config.Save();

                GoToPage(2);
            }
            else if (_page == 2)
            {
                DialogResult = true;
                Close();
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_page == 2) GoToPage(1);
        }

        private void GoToPage(int page)
        {
            _page = page;
            Page1.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
            Page2.Visibility = page == 2 ? Visibility.Visible : Visibility.Collapsed;

            BtnBack.Visibility  = page > 1 ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Content     = page == 2 ? "Start 🚀" : "Next →";

            TxtSubtitle.Text = page switch
            {
                2 => "Setup complete!",
                _ => "Which widgets do you want to use?"
            };

            // Step dots
            Dot1.Fill = new SolidColorBrush(page >= 1 ? Color.FromRgb(0x4A, 0x9E, 0xFF) : Color.FromRgb(0x2A, 0x3A, 0x4A));
            Dot2.Fill = new SolidColorBrush(page >= 2 ? Color.FromRgb(0x4A, 0x9E, 0xFF) : Color.FromRgb(0x2A, 0x3A, 0x4A));
            Dot3.Fill = new SolidColorBrush(page >= 3 ? Color.FromRgb(0x4A, 0x9E, 0xFF) : Color.FromRgb(0x2A, 0x3A, 0x4A));
        }
    }
}
