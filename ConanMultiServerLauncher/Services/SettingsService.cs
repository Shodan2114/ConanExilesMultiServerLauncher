using System;
using System.IO;
using System.Text.Json;

namespace ConanMultiServerLauncher.Services
{
    public class AppSettings
    {
        public string? ModListTxtOverride { get; set; }
        public string? ConanModsFolderOverride { get; set; }
        public string? Workshop440900Override { get; set; }
        public bool WriteModListOnProfileChange { get; set; } = false;
    }

    public static class SettingsService
    {
        private static readonly string Dir;
        private static readonly string FilePath;
        private static AppSettings? _cached;

        static SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            Dir = Path.Combine(appData, "ConanMultiServerLauncher");
            Directory.CreateDirectory(Dir);
            FilePath = Path.Combine(Dir, "settings.json");
        }

        public static AppSettings Load()
        {
            if (_cached != null) return _cached;
            if (!File.Exists(FilePath))
            {
                _cached = new AppSettings();
                return _cached;
            }
            var json = File.ReadAllText(FilePath);
            _cached = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            return _cached;
        }

        public static void Save(AppSettings settings)
        {
            _cached = settings;
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
    }
}
