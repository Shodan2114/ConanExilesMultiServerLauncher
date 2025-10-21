using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConanMultiServerLauncher.Services
{
    public static class ModListService
    {
        private static readonly Regex IdRegex = new("(?<!\\d)(\\d{6,12})(?!\\d)", RegexOptions.Compiled);
        private static readonly Regex PakIdRegex = new(@"workshop_(\\d{6,12})\\.pak", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex WorkshopPathIdRegex = new(@"(?:/|\\)440900(?:/|\\)(\\d{6,12})(?:/|\\)", RegexOptions.Compiled);

        // Extract workshop IDs from arbitrary text (URLs, numbers, etc.)
        public static List<long> ExtractModIds(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new();
            var ids = IdRegex.Matches(raw)
                .Select(m => m.Groups[1].Value)
                .Select(s => long.TryParse(s, out var id) ? id : (long?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            // Filter out Conan app id if accidentally captured (should never be treated as a mod id)
            return ids.Where(x => x != 440900).ToList();
        }

        // New: extract IDs from any pasted form (URLs, pak filenames, or workshop paths)
        public static List<long> ExtractModIdsFromAny(string raw)
        {
            var set = new HashSet<long>();
            if (string.IsNullOrWhiteSpace(raw)) return new();

            foreach (Match m in PakIdRegex.Matches(raw))
            {
                if (long.TryParse(m.Groups[1].Value, out var id)) set.Add(id);
            }
            foreach (Match m in WorkshopPathIdRegex.Matches(raw))
            {
                if (long.TryParse(m.Groups[1].Value, out var id)) set.Add(id);
            }
            foreach (var id in ExtractModIds(raw)) set.Add(id);

            return set.Where(x => x != 440900).Distinct().ToList();
        }

        // Resolve the first .pak path for a given workshop id, if installed; otherwise null
        public static string? TryGetPakPathForId(long id)
        {
            if (id == 440900) return null;
            var workshopRoot = PathsService.GetWorkshopContent440900();
            if (string.IsNullOrWhiteSpace(workshopRoot) || !Directory.Exists(workshopRoot))
                return null;
            var idFolder = Path.Combine(workshopRoot, id.ToString());
            if (!Directory.Exists(idFolder)) return null;
            var pakPath = Directory.EnumerateFiles(idFolder, "*.pak", SearchOption.AllDirectories).FirstOrDefault();
            return pakPath;
        }

        // Returns a friendly display label: "<id> — <pakName>" or "<id> — (not installed)"
        public static string GetDisplayLabelForId(long id)
        {
            var pakPath = TryGetPakPathForId(id);
            var suffix = pakPath != null ? Path.GetFileName(pakPath) : "(not installed)";
            return $"{id} — {suffix}";
        }

        // Writes absolute .pak paths into servermodlist.txt (one per line).
        // Requires a known Workshop path so we can resolve real .pak file locations.
        public static void WriteConanModListTxt(IEnumerable<long> modIds)
        {
            var idsList = modIds?.Distinct().ToList() ?? new();
            var serverModListPath = PathsService.GetConanServerModListTxt();
            if (string.IsNullOrWhiteSpace(serverModListPath))
                throw new InvalidOperationException("Conan servermodlist.txt path is unknown. Use 'Locate servermodlist.txt' to set it.");

            var workshopRoot = PathsService.GetWorkshopContent440900();
            if (string.IsNullOrWhiteSpace(workshopRoot) || !Directory.Exists(workshopRoot))
                throw new InvalidOperationException("Steam Workshop path for Conan (440900) not found. Use 'Locate Workshop 440900' to set it.");

            var serverLines = new List<string>();   // with leading '*'
            var localLines = new List<string>();    // without leading '*'

            foreach (var id in idsList)
            {
                if (id == 440900) continue; // never treat the app id as a mod id
                var idFolder = Path.Combine(workshopRoot, id.ToString());
                if (!Directory.Exists(idFolder))
                {
                    // If the mod is not installed (not subscribed or not downloaded), skip it.
                    // User can subscribe in Steam and try again.
                    continue;
                }

                // Prefer the first .pak we find under this workshop item; some mods ship multiple paks
                var pakPath = Directory.EnumerateFiles(idFolder, "*.pak", SearchOption.AllDirectories).FirstOrDefault();
                if (pakPath != null)
                {
                    // servermodlist.txt requires a leading '*'
                    serverLines.Add("*" + pakPath);
                    // LocalAppData modlist.txt must NOT have the leading '*'
                    localLines.Add(pakPath);
                }
            }

            // Write ConanSandbox\\servermodlist.txt (game install)
            Directory.CreateDirectory(Path.GetDirectoryName(serverModListPath)!);
            File.WriteAllLines(serverModListPath, serverLines);

            // Write %LocalAppData% modlist.txt (user config) WITHOUT leading '*'
            var localModListPath = PathsService.GetLocalAppDataModListTxt();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(localModListPath)!);
                File.WriteAllLines(localModListPath, localLines);
            }
            catch
            {
                // Non-fatal: if LocalAppData path fails, we still consider servermodlist.txt written
            }

            // Also write <Conan Exiles>\\ConanSandbox\\Mods\\modlist.txt (game install Mods folder) WITHOUT leading '*'
            var modsFolder = PathsService.GetConanModsFolder();
            if (!string.IsNullOrWhiteSpace(modsFolder) && Directory.Exists(modsFolder))
            {
                var modsModListPath = Path.Combine(modsFolder, "modlist.txt");
                try
                {
                    Directory.CreateDirectory(modsFolder);
                    File.WriteAllLines(modsModListPath, localLines);
                }
                catch
                {
                    // Non-fatal: if Mods folder write fails, continue
                }
            }
        }

        // Reads a .txt (for example copied from a server or your own list) and extracts IDs
        public static List<long> ReadIdsFromTextFile(string filePath)
        {
            var text = File.ReadAllText(filePath);
            return ExtractModIdsFromAny(text);
        }
    }
}