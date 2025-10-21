using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ConanMultiServerLauncher.Services
{
    public static class GameConfigService
    {
        // Updates LastConnectedServerIP/Port/Password in GameUserSettings.ini
        public static void UpdateLastConnectedServer(string? serverAddress, string? password)
        {
            var iniPath = PathsService.GetGameUserSettingsIni();
            Directory.CreateDirectory(Path.GetDirectoryName(iniPath)!);

            // Parse ip and port
            var (ip, port) = ParseServerAddress(serverAddress);

            // Read existing lines or start a new file
            var lines = File.Exists(iniPath) ? File.ReadAllLines(iniPath).ToList() : new List<string>();

            // Ensure [ServerSettings] section exists and get its range
            EnsureSection(lines, "ServerSettings");
            var (start, end) = GetSectionRange(lines, "ServerSettings");

            // Upsert keys inside the section
            UpsertKey(lines, start, end, "LastConnectedServerIP", ip ?? string.Empty);
            UpsertKey(lines, start, end, "LastConnectedServerPort", port.ToString());
            UpsertKey(lines, start, end, "LastConnectedServerPassword", password ?? string.Empty);

            File.WriteAllLines(iniPath, lines, new UTF8Encoding(false));
        }

        private static (string? ip, int port) ParseServerAddress(string? serverAddress)
        {
            const int defaultPort = 27015;
            if (string.IsNullOrWhiteSpace(serverAddress))
                return (null, defaultPort);

            var s = serverAddress.Trim();
            // If IPv6 in brackets [::1]:port not expected here, but handle simple cases
            var parts = s.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts.Last(), out var parsedPort))
            {
                var host = string.Join(":", parts.Take(parts.Length - 1));
                return (host, parsedPort);
            }
            return (s, defaultPort);
        }

        private static void EnsureSection(List<string> lines, string sectionName)
        {
            if (lines.Any(l => IsSectionLine(l, sectionName))) return;
            if (lines.Count > 0 && lines[^1].Length != 0)
                lines.Add(string.Empty);
            lines.Add($"[{sectionName}]");
            // Add an empty line after for readability
            lines.Add(string.Empty);
        }

        private static (int start, int end) GetSectionRange(List<string> lines, string sectionName)
        {
            int start = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (IsSectionLine(lines[i], sectionName)) { start = i; break; }
            }
            if (start == -1)
            {
                // Should not happen if EnsureSection was called, but guard anyway
                lines.Add($"[{sectionName}]");
                start = lines.Count - 1;
                lines.Add(string.Empty);
            }

            int end = lines.Count;
            for (int i = start + 1; i < lines.Count; i++)
            {
                if (IsAnySectionLine(lines[i])) { end = i; break; }
            }
            return (start, end);
        }

        private static bool IsSectionLine(string line, string sectionName)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            line = line.Trim();
            return line.StartsWith("[") && line.EndsWith("]") &&
                   string.Equals(line.Trim('[', ']'), sectionName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAnySectionLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            line = line.Trim();
            return line.StartsWith("[") && line.EndsWith("]");
        }

        private static void UpsertKey(List<string> lines, int start, int end, string key, string value)
        {
            // Search within section range (exclusive of section header at index 'start')
            for (int i = start + 1; i < end; i++)
            {
                var raw = lines[i];
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var idx = raw.IndexOf('=');
                if (idx <= 0) continue;
                var k = raw.Substring(0, idx).Trim();
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = key + "=" + value;
                    return;
                }
            }
            // Not found: insert before end
            lines.Insert(end, key + "=" + value);
        }
    }
}
