using System;
using System.Diagnostics;
using System.IO;

namespace ConanMultiServerLauncher.Services
{
    public static class LauncherService
    {
        // Launch Conan directly, choosing BattlEye or non-BattlEye executable, continue last session
        public static void LaunchConan(bool battlEyeEnabled, string? serverAddress = null, string? password = null)
        {
            // serverAddress/password not passed as args; we set last-connected server in GameUserSettings.ini separately
            var conanRoot = PathsService.GetConanRoot();
            if (string.IsNullOrWhiteSpace(conanRoot) || !Directory.Exists(conanRoot))
                throw new InvalidOperationException("Conan Exiles root folder not found. Make sure Conan Exiles is installed.");

            var bin64 = Path.Combine(conanRoot!, "ConanSandbox", "Binaries", "Win64");
            var exeName = battlEyeEnabled ? "ConanSandbox_BE.exe" : "ConanSandbox.exe";
            var exePath = Path.Combine(bin64, exeName);
            if (!File.Exists(exePath))
                throw new InvalidOperationException($"{exeName} not found under ConanSandbox\\Binaries\\Win64. Expected at: {exePath}");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(exePath)!
            };
            Process.Start(psi);
        }
    }
}