using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TaskbarMusicWidget.Services;

namespace TaskbarMusicWidget.Windows
{
    public class WidgetRow
    {
        public string Id          { get; set; } = "";
        public string Icon        { get; set; } = "";
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
        public bool   IsEnabled   { get; set; }
    }

    public partial class ControlPanel : Window
    {
        private readonly ObservableCollection<WidgetRow> _rows = new();
        private bool _loading = true;

        public ControlPanel()
        {
            InitializeComponent();
            WidgetList.ItemsSource = _rows;
            LoadData();
            _loading = false;
        }

        private void LoadData()
        {
            _loading = true;
            var mgr = WidgetManager.Instance;
            _rows.Clear();

            // Sort widgets by Order, then populate rows
            var sorted = mgr.Config.Widgets
                .OrderBy(wc => wc.Order)
                .ToList();

            foreach (var wc in sorted)
            {
                if (!WidgetManager.WidgetMeta.TryGetValue(wc.Id, out var meta)) continue;
                _rows.Add(new WidgetRow
                {
                    Id          = wc.Id,
                    Icon        = meta.Icon,
                    Name        = meta.Name,
                    Description = meta.Description,
                    IsEnabled   = wc.IsEnabled
                });
            }



            // Startup
            ChkStartup.IsChecked = mgr.Config.StartWithWindows;
            ChkHideFullScreen.IsChecked = mgr.Config.HideOnFullScreen;
            SldPosition.Value = mgr.Config.PositionPercent;
            _loading = false;
        }


        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Widget_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender is CheckBox cb && cb.Tag is string id)
                WidgetManager.Instance.SetWidgetEnabled(id, true);
        }

        private void Widget_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender is CheckBox cb && cb.Tag is string id)
                WidgetManager.Instance.SetWidgetEnabled(id, false);
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                WidgetManager.Instance.MoveWidgetUp(id);
                LoadData();
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                WidgetManager.Instance.MoveWidgetDown(id);
                LoadData();
            }
        }

        private void Startup_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            bool enable = ChkStartup.IsChecked == true;
            WidgetManager.Instance.Config.StartWithWindows = enable;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (enable)
                    key?.SetValue("TaskbarWidgets", $"\"{Environment.ProcessPath}\"");
                else
                    key?.DeleteValue("TaskbarWidgets", false);
            }
            catch { }
        }

        private void HideFullScreen_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            WidgetManager.Instance.Config.HideOnFullScreen = ChkHideFullScreen.IsChecked == true;
            WidgetManager.Instance.Config.Save();
        }

        private void Position_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            WidgetManager.Instance.Config.PositionPercent = e.NewValue;
            WidgetManager.Instance.Config.Save();
            
            // Force an immediate refresh of the main window position
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.ForceReposition();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var mgr = WidgetManager.Instance;


            TxtStatus.Text = "✓ Saved";
            TxtStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
    }
}
