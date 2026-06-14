using System;
using System.IO;
using System.Windows;
using System.Drawing;
using System.Threading.Tasks;
using TaskbarMusicWidget.Services;
using TaskbarMusicWidget.Windows;

namespace TaskbarMusicWidget
{
    public partial class App : Application
    {
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private ControlPanel? _controlPanel;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            InitNotifyIcon();
            SetupExceptionHandlers();

            // Check first run — wizard runs BEFORE main window opens
            var mgr = WidgetManager.Instance;
            if (mgr.Config.IsFirstRun)
            {
                var wizard = new SetupWizard();
                // ShowDialog blocks until user finishes or closes wizard
                if (wizard.ShowDialog() != true)
                {
                    // User closed wizard without completing — use defaults
                    mgr.Config.IsFirstRun = false;
                    mgr.Config.Save();
                }
            }

            // Open main widget window
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        // ── Tray icon ──────────────────────────────────────────────────────────
        private void InitNotifyIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Text    = "Taskbar Widget Collection",
                Visible = true
            };

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            _notifyIcon.Icon = File.Exists(iconPath)
                ? TryLoadIcon(iconPath)
                : System.Drawing.SystemIcons.Application;

            _notifyIcon.DoubleClick += (_, _) => OpenControlPanel();

            BuildContextMenu();
        }

        private static System.Drawing.Icon TryLoadIcon(string path)
        {
            try { return new System.Drawing.Icon(path); }
            catch { return System.Drawing.SystemIcons.Application; }
        }

        private void BuildContextMenu()
        {
            if (_notifyIcon == null) return;

            var menu = new System.Windows.Forms.ContextMenuStrip();

            // Widget switcher submenu
            var widgetsMenu = new System.Windows.Forms.ToolStripMenuItem("⬛  Widgets");
            var mgr = WidgetManager.Instance;
            var active = mgr.GetActiveWidgets();
            int i = 0;
            foreach (var id in active)
            {
                int index = i++;
                if (!WidgetManager.WidgetMeta.TryGetValue(id, out var meta)) continue;
                var item = new System.Windows.Forms.ToolStripMenuItem($"{meta.Icon}  {meta.Name}");
                item.Click += (_, _) =>
                {
                    while (mgr.CurrentIndex != index) mgr.Next();
                };
                widgetsMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(widgetsMenu);

            // Control panel
            var cpItem = new System.Windows.Forms.ToolStripMenuItem("⚙  Control Panel");
            cpItem.Click += (_, _) => Dispatcher.Invoke(OpenControlPanel);
            menu.Items.Add(cpItem);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("✕  Exit");
            exitItem.Click += (_, _) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                Current.Shutdown();
            };
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void OpenControlPanel()
        {
            if (_controlPanel != null && _controlPanel.IsVisible)
            {
                _controlPanel.Activate();
                return;
            }
            _controlPanel = new ControlPanel();
            _controlPanel.Closed += (_, _) =>
            {
                _controlPanel = null;
                // Rebuild tray menu in case widget list changed
                BuildContextMenu();
            };
            _controlPanel.Show();
        }

        public void ShowBalloonTip(string title, string message)
        {
            _notifyIcon?.ShowBalloonTip(4000, title, message, System.Windows.Forms.ToolTipIcon.Info);
        }

        // ── Exception handlers ─────────────────────────────────────────────────
        private void SetupExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                LogException(args.ExceptionObject as Exception);

            DispatcherUnhandledException += (_, args) =>
            {
                LogException(args.Exception);
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                LogException(args.Exception);
                args.SetObserved();
            };
        }

        private static void LogException(Exception? ex)
        {
            if (ex == null) return;
            try
            {
                // Write to AppData — writing to app directory in shared environments
                // can cause stack trace leaks.
                string logDir  = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TaskbarWidgets");
                Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir, "crash.log");
                File.AppendAllText(logFile,
                    $"[{DateTime.Now}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n");
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}