using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConanMultiServerLauncher.Services
{
    public static class PathsService
    {
        // 1) Steam root from registry or default
        public static string? GetSteamRoot()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\\Valve\\Steam");
            var path = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                return path;

            var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            return Directory.Exists(defaultPath) ? defaultPath : null;
        }

        public static string? GetSteamExe()
        {
            var root = GetSteamRoot();
            if (root == null) return null;
            var exe = Path.Combine(root, "steam.exe");
            return File.Exists(exe) ? exe : null;
        }

        // 2) Enumerate all Steam library roots (…\\steamapps)
        private static IEnumerable<string> EnumerateSteamAppsRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var steamRoot = GetSteamRoot();
            if (steamRoot != null)
            {
                var main = Path.Combine(steamRoot, "steamapps");
                if (Directory.Exists(main) && seen.Add(main))
                    yield return main;

                var vdf = Path.Combine(main, "libraryfolders.vdf");
                foreach (var root in ParseLibraryFolders(vdf))
                {
                    var apps = Path.Combine(root, "steamapps");
                    if (Directory.Exists(apps) && seen.Add(apps))
                        yield return apps;
                }
            }
        }

        // Parses libraryfolders.vdf for library paths (lenient)
        private static IEnumerable<string> ParseLibraryFolders(string vdfPath)
        {
            if (!File.Exists(vdfPath)) yield break;
            var text = File.ReadAllText(vdfPath);
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = trimmed.Split('"');
                if (parts.Length >= 4)
                {
                    var path = parts[3].Replace('/', '\\');
                    if (Directory.Exists(path))
                        yield return path;
                }
            }
        }

        public static string? GetConanRoot()
        {
            // Override via settings
            var settings = SettingsService.Load();
            if (!string.IsNullOrWhiteSpace(settings.ConanModsFolderOverride))
            {
                var mods = settings.ConanModsFolderOverride;
                if (Directory.Exists(mods))
                {
                    var conan = Directory.GetParent(mods)?.Parent?.FullName;
                    if (!string.IsNullOrWhiteSpace(conan) && Directory.Exists(conan))
                        return conan;
                }
            }

            // Search all libraries
            foreach (var apps in EnumerateSteamAppsRoots())
            {
                var conan = Path.Combine(apps, "common", "Conan Exiles");
                if (Directory.Exists(conan))
                    return conan;
            }
            return null;
        }

        public static string? GetConanModsFolder()
        {
            var settings = SettingsService.Load();
            if (!string.IsNullOrWhiteSpace(settings.ConanModsFolderOverride) && Directory.Exists(settings.ConanModsFolderOverride))
                return settings.ConanModsFolderOverride;

            var conan = GetConanRoot();
            if (conan == null) return null;
            var mods = Path.Combine(conan, "ConanSandbox", "Mods");
            try { Directory.CreateDirectory(mods); } catch { }
            return Directory.Exists(mods) ? mods : null;
        }

        public static string? GetConanSandboxFolder()
        {
            // Prefer override if user already selected Mods folder override
            var settings = SettingsService.Load();
            if (!string.IsNullOrWhiteSpace(settings.ConanModsFolderOverride) && Directory.Exists(settings.ConanModsFolderOverride))
            {
                var mods = settings.ConanModsFolderOverride;
                var sandbox = Directory.GetParent(mods)?.FullName;
                if (!string.IsNullOrWhiteSpace(sandbox) && Directory.Exists(sandbox))
                    return sandbox;
            }

            var conan = GetConanRoot();
            if (conan == null) return null;
            var sandboxPath = Path.Combine(conan, "ConanSandbox");
            return Directory.Exists(sandboxPath) ? sandboxPath : null;
        }

        public static string? GetConanServerModListTxt()
        {
            var settings = SettingsService.Load();
            if (!string.IsNullOrWhiteSpace(settings.ModListTxtOverride))
            {
                // If user pointed to old Mods\\modlist.txt, map to ConanSandbox\\servermodlist.txt
                try
                {
                    var selected = settings.ModListTxtOverride;
                    if (selected.EndsWith("modlist.txt", StringComparison.OrdinalIgnoreCase) && !selected.EndsWith("servermodlist.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        var modsDir = Path.GetDirectoryName(selected);
                        var sandbox = modsDir != null ? Directory.GetParent(modsDir)?.FullName : null;
                        if (!string.IsNullOrWhiteSpace(sandbox))
                            return Path.Combine(sandbox!, "servermodlist.txt");
                    }
                    if (selected.EndsWith("servermodlist.txt", StringComparison.OrdinalIgnoreCase))
                        return selected;
                }
                catch { }
            }

            var sandboxFolder = GetConanSandboxFolder();
            if (sandboxFolder == null) return null;
            return Path.Combine(sandboxFolder, "servermodlist.txt");
        }

        // Back-compat shim. Existing callers now target servermodlist.txt
        public static string? GetConanModListTxt() => GetConanServerModListTxt();

        public static string? GetWorkshopContent440900()
        {
            var settings = SettingsService.Load();
            if (!string.IsNullOrWhiteSpace(settings.Workshop440900Override) && Directory.Exists(settings.Workshop440900Override))
                return settings.Workshop440900Override;

            foreach (var apps in EnumerateSteamAppsRoots())
            {
                var p = Path.Combine(apps, "workshop", "content", "440900");
                if (Directory.Exists(p))
                    return p;
            }
            return null;
        }

        // Resolve Funcom Launcher executable
        public static string? GetFuncomLauncherExe()
        {
            var root = GetConanRoot();
            if (string.IsNullOrWhiteSpace(root)) return null;
            var launcher = Path.Combine(root, "Launcher", "FuncomLauncher.exe");
            return File.Exists(launcher) ? launcher : null;
        }

        // Resolve path to GameUserSettings.ini under LocalAppData
        public static string GetGameUserSettingsIni()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var iniDir = Path.Combine(localAppData, "ConanSandbox", "Saved", "Config", "WindowsNoEditor");
            Directory.CreateDirectory(iniDir);
            return Path.Combine(iniDir, "GameUserSettings.ini");
        }

        // Resolve LocalAppData Conan config directory and modlist.txt path
        public static string GetLocalAppDataConanConfigDir()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(localAppData, "ConanSandbox", "Saved", "Config", "WindowsNoEditor");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetLocalAppDataModListTxt()
        {
            var dir = GetLocalAppDataConanConfigDir();
            return Path.Combine(dir, "modlist.txt");
        }
    }
}