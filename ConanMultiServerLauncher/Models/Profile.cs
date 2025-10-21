using System.Collections.Generic;

namespace ConanMultiServerLauncher.Models
{
    public class Profile
    {
        public string Name { get; set; } = string.Empty;            // Display name for the profile
        public string? ServerAddress { get; set; }                   // e.g. "123.45.67.89:7777" or hostname:port
        public string? Password { get; set; }
        public List<long> ModIds { get; set; } = new();             // Steam Workshop item IDs
        public bool BattlEyeEnabled { get; set; } = false;          // If true launch ConanSandbox_BE.exe, else ConanSandbox.exe
    }
}