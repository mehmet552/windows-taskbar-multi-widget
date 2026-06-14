using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskbarMusicWidget.Models
{
    public class WidgetConfig
    {
        public string Id { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int Order { get; set; }
    }

    public class PomodoroSettings
    {
        public int WorkMinutes { get; set; } = 25;
        public int ShortBreakMinutes { get; set; } = 5;
        public int LongBreakMinutes { get; set; } = 15;
        public int SessionsBeforeLongBreak { get; set; } = 4;
    }

    public class AppConfig
    {
        public bool IsFirstRun { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public int ActiveWidgetIndex { get; set; } = 0;
        public List<WidgetConfig> Widgets { get; set; } = new();
        public PomodoroSettings Pomodoro { get; set; } = new();

        [JsonIgnore]
        private static readonly string _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskbarWidgets");

        [JsonIgnore]
        private static string ConfigPath => Path.Combine(_configDir, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch { }
            return CreateDefault();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(_configDir);
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, opts));
            }
            catch { }
        }

        public static AppConfig CreateDefault() => new()
        {
            IsFirstRun = true,
            Widgets = new List<WidgetConfig>
            {
                new() { Id = "Music",     IsEnabled = true, Order = 0 },
                new() { Id = "Timer",     IsEnabled = true, Order = 1 },
                new() { Id = "Stopwatch", IsEnabled = true, Order = 2 },
                new() { Id = "Pomodoro",  IsEnabled = true, Order = 3 },
            }
        };
    }
}
