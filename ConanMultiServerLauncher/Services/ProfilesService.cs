using ConanMultiServerLauncher.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ConanMultiServerLauncher.Services
{
    public class ProfilesService
    {
        private readonly string _profilesPath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        public ProfilesService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "ConanMultiServerLauncher");
            Directory.CreateDirectory(dir);
            _profilesPath = Path.Combine(dir, "profiles.json");
        }

        public List<Profile> Load()
        {
            if (!File.Exists(_profilesPath)) return new();
            try
            {
                var json = File.ReadAllText(_profilesPath);
                var list = JsonSerializer.Deserialize<List<Profile>>(json) ?? new();
                // Clean: drop empty names and deduplicate by name (case-insensitive), keep last occurrence
                var cleaned = list
                    .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name))
                    .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Last())
                    .ToList();
                return cleaned;
            }
            catch
            {
                // If file is corrupt, return empty list instead of crashing UI
                return new();
            }
        }

        public void Save(List<Profile> profiles)
        {
            // Clean before save: ignore empty names and dedupe by name (case-insensitive)
            var cleaned = (profiles ?? new List<Profile>())
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name))
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var json = JsonSerializer.Serialize(cleaned, _jsonOptions);
            File.WriteAllText(_profilesPath, json);
        }
    }
}