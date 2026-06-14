using System;
using System.Collections.Generic;
using System.Linq;
using TaskbarMusicWidget.Models;

namespace TaskbarMusicWidget.Services
{
    public class WidgetManager
    {
        private static WidgetManager? _instance;
        public static WidgetManager Instance => _instance ??= new WidgetManager();

        public AppConfig Config { get; private set; }

        public static readonly string[] AllWidgetIds = { "Music", "Timer", "Stopwatch", "Pomodoro" };

        public static readonly Dictionary<string, (string Icon, string Name, string Description)> WidgetMeta =
            new()
            {
                ["Music"]     = ("🎵", "Music",      "Shows and controls currently playing music"),
                ["Timer"]     = ("⏱",  "Timer", "Countdown timer (right-click to set time)"),
                ["Stopwatch"] = ("⏱",  "Stopwatch",  "Precision timer and lap tracking"),
                ["Pomodoro"]  = ("🍅", "Pomodoro",    "25/5/15 min focus + break cycle"),
            };

        private int _currentIndex;

        private WidgetManager()
        {
            Config = AppConfig.Load();
            _currentIndex = Config.ActiveWidgetIndex;
            ClampIndex();
        }

        private void ClampIndex()
        {
            var active = GetActiveWidgets();
            if (active.Count == 0) { _currentIndex = 0; return; }
            if (_currentIndex >= active.Count) _currentIndex = active.Count - 1;
            if (_currentIndex < 0) _currentIndex = 0;
        }

        public void Reload()
        {
            Config = AppConfig.Load();
            _currentIndex = Config.ActiveWidgetIndex;
            ClampIndex();
        }

        public List<string> GetActiveWidgets() =>
            Config.Widgets
                .Where(w => w.IsEnabled)
                .OrderBy(w => w.Order)
                .Select(w => w.Id)
                .ToList();

        public int CurrentIndex => _currentIndex;

        public string? CurrentWidgetId
        {
            get
            {
                var active = GetActiveWidgets();
                if (active.Count == 0) return null;
                return active[Math.Min(_currentIndex, active.Count - 1)];
            }
        }

        public bool CanNavigate => GetActiveWidgets().Count > 1;

        public event Action? WidgetChanged;
        public event Action? ConfigChanged;

        public void Next()
        {
            var active = GetActiveWidgets();
            if (active.Count <= 1) return;
            _currentIndex = (_currentIndex + 1) % active.Count;
            Config.ActiveWidgetIndex = _currentIndex;
            Config.Save();
            WidgetChanged?.Invoke();
        }

        public void Previous()
        {
            var active = GetActiveWidgets();
            if (active.Count <= 1) return;
            _currentIndex = (_currentIndex - 1 + active.Count) % active.Count;
            Config.ActiveWidgetIndex = _currentIndex;
            Config.Save();
            WidgetChanged?.Invoke();
        }

        public void SetWidgetEnabled(string id, bool enabled)
        {
            var w = Config.Widgets.FirstOrDefault(x => x.Id == id);
            if (w == null) return;
            w.IsEnabled = enabled;
            ClampIndex();
            Config.ActiveWidgetIndex = _currentIndex;
            Config.Save();
            ConfigChanged?.Invoke();
        }

        public void MoveWidgetUp(string id)
        {
            var list = Config.Widgets.OrderBy(w => w.Order).ToList();
            int i = list.FindIndex(w => w.Id == id);
            if (i <= 0) return;
            (list[i].Order, list[i - 1].Order) = (list[i - 1].Order, list[i].Order);
            Config.Save();
            ConfigChanged?.Invoke();
        }

        public void MoveWidgetDown(string id)
        {
            var list = Config.Widgets.OrderBy(w => w.Order).ToList();
            int i = list.FindIndex(w => w.Id == id);
            if (i < 0 || i >= list.Count - 1) return;
            (list[i].Order, list[i + 1].Order) = (list[i + 1].Order, list[i].Order);
            Config.Save();
            ConfigChanged?.Invoke();
        }
    }
}
