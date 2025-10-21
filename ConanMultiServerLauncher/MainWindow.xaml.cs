using ConanMultiServerLauncher.Models;
using ConanMultiServerLauncher.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.IO;

namespace ConanMultiServerLauncher
{
    public partial class MainWindow : Window
    {
        private readonly ProfilesService _profilesService = new();
        private ObservableCollection<Profile> _profiles = new();
        private Profile? _current;

        public MainWindow()
        {
            InitializeComponent();
            LoadProfiles();
            RefreshModListPathLabel();
        }

        private void LoadProfiles()
        {
            _profiles = new ObservableCollection<Profile>(_profilesService.Load());
            ProfilesCombo.ItemsSource = _profiles;
            ProfilesCombo.IsEnabled = _profiles.Count > 0;
            if (_profiles.Count > 0)
            {
                ProfilesCombo.SelectedIndex = 0;
                SetCurrent(_profiles[0]);
            }
            else
            {
                _current = null;
                ProfileName.Text = string.Empty;
                ServerAddress.Text = string.Empty;
                ServerPassword.Password = string.Empty;
                ModsList.ItemsSource = null;
                ModsCountText.Text = "Mods: 0";
                if (BattlEyeCheckbox != null) BattlEyeCheckbox.IsChecked = false;
            }
        }

        private void SetCurrent(Profile p)
        {
            _current = p;
            ProfileName.Text = p.Name;
            ServerAddress.Text = p.ServerAddress ?? string.Empty;
            ServerPassword.Password = p.Password ?? string.Empty;
            ModsList.ItemsSource = p.ModIds.Select(id => ModListService.GetDisplayLabelForId(id)).ToList();
            ModsCountText.Text = $"Mods: {p.ModIds.Count}";
            if (BattlEyeCheckbox != null) BattlEyeCheckbox.IsChecked = p.BattlEyeEnabled;
        }

        private void NewProfile_Click(object sender, RoutedEventArgs e)
        {
            // Generate a unique default name to avoid accidental overwrites when saving (upsert-by-name)
            var baseName = "New Profile";
            var name = baseName;
            int i = 1;
            var existingNames = new HashSet<string>(_profiles.Select(pr => pr.Name), StringComparer.OrdinalIgnoreCase);
            while (existingNames.Contains(name))
            {
                name = $"{baseName} {i++}";
            }

            var p = new Profile { Name = name };
            _profiles.Add(p);
            ProfilesCombo.SelectedItem = p;
            SetCurrent(p);
            ProfileName.Text = p.Name;
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            // Upsert by Name: if a profile with this name exists, override it; otherwise add new.
            var name = ProfileName.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                System.Windows.MessageBox.Show("Profile Name cannot be empty.");
                return;
            }

            var serverAddr = string.IsNullOrWhiteSpace(ServerAddress.Text) ? null : ServerAddress.Text.Trim();
            var password = string.IsNullOrWhiteSpace(ServerPassword.Password) ? null : ServerPassword.Password;
            var mods = _current?.ModIds?.ToList() ?? new List<long>();

            var existing = _profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            var isUpdate = existing != null;

            if (isUpdate && existing != null)
            {
                // Update existing profile fields
                existing.ServerAddress = serverAddr;
                existing.Password = password;
                existing.ModIds = mods.Distinct().ToList();
                existing.BattlEyeEnabled = BattlEyeCheckbox?.IsChecked == true;
                existing.Name = name; // keep normalized casing

                // Remove any duplicates with same name except this one
                for (int i = _profiles.Count - 1; i >= 0; i--)
                {
                    var item = _profiles[i];
                    if (ReferenceEquals(item, existing)) continue;
                    if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                        _profiles.RemoveAt(i);
                }
                ProfilesCombo.SelectedItem = existing;
                SetCurrent(existing);
            }
            else
            {
                // Create new profile
                var newProfile = new Profile
                {
                    Name = name,
                    ServerAddress = serverAddr,
                    Password = password,
                    ModIds = mods.Distinct().ToList(),
                    BattlEyeEnabled = BattlEyeCheckbox?.IsChecked == true
                };
                _profiles.Add(newProfile);
                ProfilesCombo.SelectedItem = newProfile;
                SetCurrent(newProfile);
            }

            // Persist
            _profilesService.Save(_profiles.ToList());
            System.Windows.MessageBox.Show(isUpdate ? "Profile updated." : "Profile created.");
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            if (System.Windows.MessageBox.Show($"Delete profile '{_current.Name}'?", "Confirm", System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
            {
                _profiles.Remove(_current);
                _profilesService.Save(_profiles.ToList());
                if (_profiles.Count > 0)
                {
                    ProfilesCombo.SelectedIndex = 0;
                    SetCurrent(_profiles[0]);
                }
                else
                {
                    _current = null;
                    ProfileName.Text = string.Empty;
                    ServerAddress.Text = string.Empty;
                    ServerPassword.Password = string.Empty;
                    ModsList.ItemsSource = null;
                    ModsCountText.Text = "Mods: 0";
                }
            }
        }

        private void PasteMods_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            if (!System.Windows.Clipboard.ContainsText())
            {
                System.Windows.MessageBox.Show("Clipboard does not contain text.");
                return;
            }
            var text = System.Windows.Clipboard.GetText();
            // Use tolerant parser: supports URLs, .pak filenames, and workshop paths
            var ids = ModListService.ExtractModIdsFromAny(text);
            if (ids.Count == 0)
            {
                System.Windows.MessageBox.Show("No Workshop IDs found. Tip: paste Steam Workshop URLs/IDs, .pak filenames (e.g. workshop_1234567890.pak), or full .pak paths.");
                return;
            }
            MergeMods(ids);
        }

        private void LoadModsFromFile_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                var ids = ModListService.ReadIdsFromTextFile(ofd.FileName);
                if (ids.Count == 0)
                {
                    System.Windows.MessageBox.Show("No Workshop IDs found in file. Tip: include URLs/IDs or .pak filenames/paths.");
                    return;
                }
                MergeMods(ids);
            }
        }

        private async void PasteCollection_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            if (!System.Windows.Clipboard.ContainsText())
            {
                System.Windows.MessageBox.Show("Clipboard does not contain text.");
                return;
            }
            var text = System.Windows.Clipboard.GetText();
            if (!SteamWorkshopService.TryExtractCollectionId(text, out var collectionId))
            {
                System.Windows.MessageBox.Show("Clipboard does not contain a Steam Workshop collection URL/ID.");
                return;
            }
            try
            {
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                var ids = await SteamWorkshopService.GetCollectionChildrenAsync(collectionId);
                // Optionally filter to Conan Exiles only
                var filtered = await SteamWorkshopService.FilterToConanAsync(ids);
                var finalIds = filtered.Count > 0 ? filtered : ids;
                if (finalIds.Count == 0)
                {
                    System.Windows.MessageBox.Show("The collection contains no items (or none for Conan Exiles).");
                    return;
                }
                MergeMods(finalIds);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to expand collection: {ex.Message}");
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
            }
        }

        private void ClearMods_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            _current.ModIds.Clear();
            ModsList.ItemsSource = _current.ModIds.Select(id => ModListService.GetDisplayLabelForId(id)).ToList();
            ModsCountText.Text = "Mods: 0";
        }

        private void MergeMods(IEnumerable<long> ids)
        {
            if (_current == null) return;
            var set = new HashSet<long>(_current.ModIds);
            var added = 0;
            foreach (var id in ids)
            {
                if (set.Add(id))
                {
                    _current.ModIds.Add(id);
                    added++;
                }
            }
            _current.ModIds = _current.ModIds.Distinct().ToList();
            ModsList.ItemsSource = _current.ModIds.Select(id => ModListService.GetDisplayLabelForId(id)).ToList();
            ModsCountText.Text = $"Mods: {_current.ModIds.Count} (added {added})";
        }

        private void RefreshModListPathLabel()
        {
            if (CurrentModListPath != null)
            {
                CurrentModListPath.Text = PathsService.GetConanServerModListTxt() ?? "servermodlist.txt: not set";
            }
            if (CurrentLocalModListPath != null)
            {
                try
                {
                    var localPath = PathsService.GetLocalAppDataModListTxt();
                    CurrentLocalModListPath.Text = string.IsNullOrWhiteSpace(localPath) ? "modlist.txt: not found" : localPath;
                }
                catch
                {
                    CurrentLocalModListPath.Text = "modlist.txt: not found";
                }
            }
        }

        private void LocateModList_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Conan servermodlist.txt",
                Filter = "servermodlist.txt|servermodlist.txt|All files (*.*)|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                var settings = SettingsService.Load();
                settings.ModListTxtOverride = ofd.FileName;
                try
                {
                    var sandbox = System.IO.Path.GetDirectoryName(ofd.FileName);
                    if (!string.IsNullOrWhiteSpace(sandbox))
                    {
                        var modsDir = System.IO.Path.Combine(sandbox!, "Mods");
                        if (System.IO.Directory.Exists(modsDir))
                            settings.ConanModsFolderOverride = modsDir;
                    }
                }
                catch { }
                SettingsService.Save(settings);
                RefreshModListPathLabel();
                System.Windows.MessageBox.Show("servermodlist location saved.");
            }
        }

        private void LocateWorkshop_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Steam Workshop folder for Conan (…\\steamapps\\workshop\\content\\440900)"
            };
            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                var selected = dlg.SelectedPath;
                var settings = SettingsService.Load();
                settings.Workshop440900Override = selected;
                SettingsService.Save(settings);
                System.Windows.MessageBox.Show("Workshop path saved.");
            }
        }

        private void WriteModList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_current == null) return;
                ModListService.WriteConanModListTxt(_current.ModIds);
                System.Windows.MessageBox.Show("servermodlist.txt and modlist.txt updated.");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to write servermodlist.txt: {ex.Message}\n\nTip: Click 'Locate servermodlist.txt' to set its location if auto-detection fails.");
            }
        }

        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_current == null)
                {
                    System.Windows.MessageBox.Show("Create or select a profile first.");
                    return;
                }

                // Ensure modlist is written before launch
                ModListService.WriteConanModListTxt(_current.ModIds);

                // Update last-connected server in GameUserSettings.ini so the launcher can connect
                if (!string.IsNullOrWhiteSpace(_current.ServerAddress))
                {
                    GameConfigService.UpdateLastConnectedServer(_current.ServerAddress, _current.Password);
                }

                LauncherService.LaunchConan(_current.BattlEyeEnabled, _current.ServerAddress, _current.Password);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to launch Conan: {ex.Message}");
            }
        }

        private void OpenProfilesFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dir = Path.Combine(appData, "ConanMultiServerLauncher");
                Directory.CreateDirectory(dir);
                // Open in Explorer
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open Profiles folder: {ex.Message}");
            }
        }

        private void ProfilesCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ProfilesCombo.SelectedItem is Profile p)
                SetCurrent(p);
        }
    }
}
