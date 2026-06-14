using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace TaskbarMusicWidget.Windows
{
    public partial class CustomTimeDialog : Window
    {
        public TimeSpan? SelectedTime { get; private set; }

        public CustomTimeDialog()
        {
            InitializeComponent();
        }

        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            int h = 0, m = 0, s = 0;
            bool ok = int.TryParse(TxtHours.Text, out h) &&
                      int.TryParse(TxtMinutes.Text, out m) &&
                      int.TryParse(TxtSeconds.Text, out s);
            if (!ok || (h == 0 && m == 0 && s == 0))
            {
                TxtError.Text = "Enter a valid duration.";
                TxtError.Visibility = Visibility.Visible;
                return;
            }
            SelectedTime = new TimeSpan(h, m, s);
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
