using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using TaskbarMusicWidget.Services;

namespace TaskbarMusicWidget.Windows
{
    public partial class PomodoroSettingsDialog : Window
    {
        private static readonly Regex _numRegex = new Regex("[^0-9]+");

        public PomodoroSettingsDialog()
        {
            InitializeComponent();
            var p = WidgetManager.Instance.Config.Pomodoro;
            TxtWork.Text   = p.WorkMinutes.ToString();
            TxtShort.Text  = p.ShortBreakMinutes.ToString();
            TxtLong.Text   = p.LongBreakMinutes.ToString();
            TxtCycles.Text = p.SessionsBeforeLongBreak.ToString();
        }

        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = _numRegex.IsMatch(e.Text);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtWork.Text, out int w) && w > 0 &&
                int.TryParse(TxtShort.Text, out int s) && s > 0 &&
                int.TryParse(TxtLong.Text, out int l) && l > 0 &&
                int.TryParse(TxtCycles.Text, out int c) && c > 0)
            {
                var mgr = WidgetManager.Instance;
                var p = mgr.Config.Pomodoro;
                p.WorkMinutes = w;
                p.ShortBreakMinutes = s;
                p.LongBreakMinutes = l;
                p.SessionsBeforeLongBreak = c;
                mgr.Config.Save();

                DialogResult = true;
                Close();
            }
            else
            {
                TxtError.Text = "Please enter valid numbers > 0.";
                TxtError.Visibility = Visibility.Visible;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
