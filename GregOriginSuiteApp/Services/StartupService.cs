using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GregOriginSuiteApp.Models;
using Microsoft.Win32;

namespace GregOriginSuiteApp.Services
{
    public sealed class StartupBackupEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Action { get; set; } = "";
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public string Location { get; set; } = "";
        public string RegistryHive { get; set; } = "";
        public string RegistryPath { get; set; } = "";
        public string DisabledRegistryPath { get; set; } = "";
        public RegistryValueKind RegistryValueKind { get; set; } = RegistryValueKind.String;
        public string OriginalFilePath { get; set; } = "";
        public string DisabledFilePath { get; set; } = "";
        public string FileBackupPath { get; set; } = "";
        public string EntryType { get; set; } = "";
        public string ScheduledTaskName { get; set; } = "";
        public bool Restored { get; set; }

        public static StartupBackupEntry FromStartupApp(StartupApp app, string action)
        {
            return new StartupBackupEntry
            {
                Action = action,
                Name = app.Name,
                Command = app.Command,
                Location = app.Location,
                RegistryHive = app.RegistryHive,
                RegistryPath = app.RegistryPath,
                RegistryValueKind = app.RegistryValueKind,
                OriginalFilePath = app.IsRegistry || app.IsScheduledTask ? "" : app.Key,
                EntryType = app.EntryType,
                ScheduledTaskName = app.IsScheduledTask ? app.Key : ""
            };
        }
    }

    public sealed class StartupService
    {
        private readonly IProcessRunner _runner;

        public StartupService(IProcessRunner? runner = null)
        {
            _runner = runner ?? new ProcessRunner();
        }

        public async Task<IReadOnlyList<StartupApp>> LoadAsync()
        {
            var apps = await Task.Run(() =>
            {
                var apps = new List<StartupApp>();
                LoadRegistryStartup(apps);
                LoadFolderStartup(apps);
                return apps;
            });

            apps.AddRange(await LoadScheduledStartupTasksAsync());
            return apps.OrderBy(a => a.Location).ThenBy(a => a.Name).ToList();
        }

        public async Task<OperationResult> ToggleAsync(IEnumerable<StartupApp> apps)
        {
            var result = new OperationResult();
            foreach (var app in apps)
            {
                try
                {
                    if (app.IsRegistry)
                    {
                        ToggleRegistry(app, result);
                    }
                    else if (app.IsScheduledTask)
                    {
                        await ToggleScheduledTaskAsync(app, result);
                    }
                    else
                    {
                        ToggleFile(app, result);
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{app.Name}: {ex.Message}");
                }
            }

            await Task.CompletedTask;
            return result;
        }

        public async Task<OperationResult> DeleteAsync(IEnumerable<StartupApp> apps)
        {
            var result = new OperationResult();
            foreach (var app in apps)
            {
                try
                {
                    var backup = StartupBackupEntry.FromStartupApp(app, "Deleted");
                    if (app.IsRegistry)
                    {
                        SaveBackup(backup);
                        RegistryKey hive = GetHive(app.RegistryHive);
                        using RegistryKey? key = hive.OpenSubKey(app.RegistryPath, writable: true);
                        key?.DeleteValue(app.Name, throwOnMissingValue: false);
                        result.Messages.Add($"{app.Name}: deleted from startup registry and backup saved.");
                    }
                    else if (app.IsScheduledTask)
                    {
                        await DeleteScheduledTaskAsync(app, backup, result);
                    }
                    else
                    {
                        if (File.Exists(app.Key))
                        {
                            string backupDir = Path.Combine(AppPaths.BackupDirectory, "StartupFiles");
                            Directory.CreateDirectory(backupDir);
                            string backupPath = Path.Combine(backupDir, backup.Id + Path.GetExtension(app.Key));
                            File.Copy(app.Key, backupPath, overwrite: true);
                            backup.FileBackupPath = backupPath;
                            SaveBackup(backup);
                            File.Delete(app.Key);
                            result.Messages.Add($"{app.Name}: deleted from startup folder and backup saved.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{app.Name}: {ex.Message}");
                }
            }

            await Task.CompletedTask;
            return result;
        }

        public async Task<OperationResult> RestoreBackupsAsync()
        {
            var result = new OperationResult();
            var backups = LoadBackups();
            foreach (var backup in backups.Where(b => !b.Restored).OrderByDescending(b => b.CreatedAt))
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(backup.RegistryHive))
                    {
                        RestoreRegistryBackup(backup);
                    }
                    else if (backup.EntryType == "ScheduledTask")
                    {
                        await RestoreScheduledTaskBackupAsync(backup);
                    }
                    else
                    {
                        RestoreFileBackup(backup);
                    }

                    backup.Restored = true;
                    result.Messages.Add($"{backup.Name}: restored startup backup created {backup.CreatedAt:g}.");
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{backup.Name}: restore failed: {ex.Message}");
                }
            }

            SaveBackups(backups);
            await Task.CompletedTask;
            return result;
        }

        private static void LoadRegistryStartup(List<StartupApp> apps)
        {
            var locations = new[]
            {
                new StartupRegistryLocation(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU Run", false),
                new StartupRegistryLocation(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM Run", false),
                new StartupRegistryLocation(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU RunOnce", false),
                new StartupRegistryLocation(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM RunOnce", false),
                new StartupRegistryLocation(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run_Disabled", "HKCU Run", true),
                new StartupRegistryLocation(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run_Disabled", "HKLM Run", true),
                new StartupRegistryLocation(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\RunOnce_Disabled", "HKCU RunOnce", true),
                new StartupRegistryLocation(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce_Disabled", "HKLM RunOnce", true),
                new StartupRegistryLocation(Registry.CurrentUser, "HKCU", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKCU WOW64 Run", false),
                new StartupRegistryLocation(Registry.LocalMachine, "HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM WOW64 Run", false),
                new StartupRegistryLocation(Registry.CurrentUser, "HKCU", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU WOW64 RunOnce", false),
                new StartupRegistryLocation(Registry.LocalMachine, "HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM WOW64 RunOnce", false),
                new StartupRegistryLocation(Registry.CurrentUser, "HKCU", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run_Disabled", "HKCU WOW64 Run", true),
                new StartupRegistryLocation(Registry.LocalMachine, "HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run_Disabled", "HKLM WOW64 Run", true),
                new StartupRegistryLocation(Registry.CurrentUser, "HKCU", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce_Disabled", "HKCU WOW64 RunOnce", true),
                new StartupRegistryLocation(Registry.LocalMachine, "HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce_Disabled", "HKLM WOW64 RunOnce", true)
            };

            foreach (var loc in locations)
            {
                using RegistryKey? key = loc.Hive.OpenSubKey(loc.Path);
                if (key == null)
                {
                    continue;
                }

                foreach (string valName in key.GetValueNames())
                {
                    object? value = key.GetValue(valName, "");
                    apps.Add(new StartupApp
                    {
                        Name = valName,
                        Command = value?.ToString() ?? "",
                        Location = loc.DisplayName,
                        State = loc.Disabled ? "Disabled" : "Enabled",
                        Key = $"{loc.HiveName}\\{loc.Path}",
                        RegistryHive = loc.HiveName,
                        RegistryPath = loc.Path,
                        RegistryValueKind = SafeValueKind(key, valName),
                        EntryType = "Registry"
                    });
                }
            }
        }

        private static void LoadFolderStartup(List<StartupApp> apps)
        {
            var folders = new[]
            {
                new { Path = Environment.GetFolderPath(Environment.SpecialFolder.Startup), Name = "User Startup Folder" },
                new { Path = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), Name = "All Users Startup Folder" }
            };

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder.Path))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(folder.Path).Where(path => !path.EndsWith("_disabled", StringComparison.OrdinalIgnoreCase)))
                {
                    apps.Add(new StartupApp
                    {
                        Name = Path.GetFileName(file),
                        Command = file,
                        Location = folder.Name,
                        State = "Enabled",
                        Key = file,
                        EntryType = "File"
                    });
                }

                foreach (var file in Directory.GetFiles(folder.Path, "*_disabled"))
                {
                    apps.Add(new StartupApp
                    {
                        Name = Path.GetFileName(file),
                        Command = file,
                        Location = folder.Name,
                        State = "Disabled",
                        Key = file,
                        EntryType = "File"
                    });
                }
            }
        }

        private async Task<IReadOnlyList<StartupApp>> LoadScheduledStartupTasksAsync()
        {
            var result = await _runner.RunAsync(new CommandSpec
            {
                FileName = "schtasks.exe",
                Arguments = "/Query /FO CSV /V",
                CaptureOutput = true
            });

            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return Array.Empty<StartupApp>();
            }

            return ParseScheduledTaskCsv(result.StandardOutput);
        }

        private static void ToggleRegistry(StartupApp app, OperationResult result)
        {
            RegistryKey hive = GetHive(app.RegistryHive);
            string sourcePath = app.RegistryPath;
            string targetPath = app.State == "Enabled"
                ? sourcePath + "_Disabled"
                : sourcePath.Replace("_Disabled", "");

            var backup = StartupBackupEntry.FromStartupApp(app, app.State == "Enabled" ? "Disabled" : "Enabled");
            backup.DisabledRegistryPath = app.State == "Enabled" ? targetPath : sourcePath;
            if (app.State == "Enabled")
            {
                SaveBackup(backup);
            }

            using (RegistryKey? sourceKey = hive.OpenSubKey(sourcePath, writable: true))
            using (RegistryKey? targetKey = hive.CreateSubKey(targetPath))
            {
                object? value = sourceKey?.GetValue(app.Name, app.Command);
                RegistryValueKind kind = sourceKey != null ? SafeValueKind(sourceKey, app.Name) : app.RegistryValueKind;
                targetKey?.SetValue(app.Name, value ?? app.Command, kind);
                sourceKey?.DeleteValue(app.Name, throwOnMissingValue: false);
            }

            result.Messages.Add($"{app.Name}: {(app.State == "Enabled" ? "disabled" : "enabled")} with registry value kind preserved.");
        }

        private static void ToggleFile(StartupApp app, OperationResult result)
        {
            if (app.State == "Enabled")
            {
                var backup = StartupBackupEntry.FromStartupApp(app, "Disabled");
                backup.DisabledFilePath = app.Key + "_disabled";
                SaveBackup(backup);
                File.Move(app.Key, backup.DisabledFilePath);
                result.Messages.Add($"{app.Name}: disabled by renaming startup shortcut.");
            }
            else
            {
                string restoredPath = app.Key.Replace("_disabled", "");
                File.Move(app.Key, restoredPath);
                result.Messages.Add($"{app.Name}: enabled by restoring startup shortcut name.");
            }
        }

        private async Task ToggleScheduledTaskAsync(StartupApp app, OperationResult result)
        {
            var backup = StartupBackupEntry.FromStartupApp(app, app.State == "Enabled" ? "Disabled" : "Enabled");
            if (app.State == "Enabled")
            {
                SaveBackup(backup);
            }

            string action = app.State == "Enabled" ? "/Disable" : "/Enable";
            CommandResult commandResult = await _runner.RunAsync(new CommandSpec
            {
                FileName = "schtasks.exe",
                Arguments = $"/Change /TN {CommandLineParser.Quote(app.Key)} {action}",
                CaptureOutput = true
            });
            result.AddResult(commandResult, $"{app.Name} scheduled task {(app.State == "Enabled" ? "disable" : "enable")}");
        }

        private async Task DeleteScheduledTaskAsync(StartupApp app, StartupBackupEntry backup, OperationResult result)
        {
            string backupDir = Path.Combine(AppPaths.BackupDirectory, "ScheduledTasks");
            Directory.CreateDirectory(backupDir);
            string xmlPath = Path.Combine(backupDir, backup.Id + ".xml");

            CommandResult export = await _runner.RunAsync(new CommandSpec
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN {CommandLineParser.Quote(app.Key)} /XML",
                CaptureOutput = true
            });
            if (export.Success && !string.IsNullOrWhiteSpace(export.StandardOutput))
            {
                File.WriteAllText(xmlPath, export.StandardOutput);
                backup.FileBackupPath = xmlPath;
            }

            SaveBackup(backup);
            CommandResult delete = await _runner.RunAsync(new CommandSpec
            {
                FileName = "schtasks.exe",
                Arguments = $"/Delete /TN {CommandLineParser.Quote(app.Key)} /F",
                CaptureOutput = true
            });
            result.AddResult(delete, $"{app.Name} scheduled task delete");
        }

        private static void RestoreRegistryBackup(StartupBackupEntry backup)
        {
            RegistryKey hive = GetHive(backup.RegistryHive);
            using RegistryKey? key = hive.CreateSubKey(backup.RegistryPath);
            key?.SetValue(backup.Name, backup.Command, backup.RegistryValueKind);

            if (!string.IsNullOrWhiteSpace(backup.DisabledRegistryPath))
            {
                using RegistryKey? disabledKey = hive.OpenSubKey(backup.DisabledRegistryPath, writable: true);
                disabledKey?.DeleteValue(backup.Name, throwOnMissingValue: false);
            }
        }

        private static void RestoreFileBackup(StartupBackupEntry backup)
        {
            if (!string.IsNullOrWhiteSpace(backup.FileBackupPath) && File.Exists(backup.FileBackupPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backup.OriginalFilePath) ?? AppPaths.BackupDirectory);
                File.Copy(backup.FileBackupPath, backup.OriginalFilePath, overwrite: true);
                return;
            }

            if (!string.IsNullOrWhiteSpace(backup.DisabledFilePath) && File.Exists(backup.DisabledFilePath))
            {
                File.Move(backup.DisabledFilePath, backup.OriginalFilePath, overwrite: true);
            }
        }

        private async Task RestoreScheduledTaskBackupAsync(StartupBackupEntry backup)
        {
            CommandResult result;
            if (backup.Action == "Disabled")
            {
                result = await _runner.RunAsync(new CommandSpec
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Change /TN {CommandLineParser.Quote(backup.ScheduledTaskName)} /Enable",
                    CaptureOutput = true
                });
                if (!result.Success)
                {
                    throw new InvalidOperationException($"schtasks enable failed with exit code {result.ExitCode}: {result.StandardError}");
                }
                return;
            }

            if (backup.Action == "Deleted" && File.Exists(backup.FileBackupPath))
            {
                result = await _runner.RunAsync(new CommandSpec
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Create /TN {CommandLineParser.Quote(backup.ScheduledTaskName)} /XML {CommandLineParser.Quote(backup.FileBackupPath)} /F",
                    CaptureOutput = true
                });
                if (!result.Success)
                {
                    throw new InvalidOperationException($"schtasks create failed with exit code {result.ExitCode}: {result.StandardError}");
                }
                return;
            }

            throw new InvalidOperationException("Scheduled task backup does not contain a restorable XML export.");
        }

        private static IReadOnlyList<StartupApp> ParseScheduledTaskCsv(string csv)
        {
            var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                return Array.Empty<StartupApp>();
            }

            var headers = ParseCsvLine(lines[0]);
            int taskNameIndex = headers.FindIndex(h => h.Equals("TaskName", StringComparison.OrdinalIgnoreCase));
            int taskToRunIndex = headers.FindIndex(h => h.Equals("Task To Run", StringComparison.OrdinalIgnoreCase));
            int scheduleTypeIndex = headers.FindIndex(h => h.Equals("Schedule Type", StringComparison.OrdinalIgnoreCase));
            int stateIndex = headers.FindIndex(h => h.Equals("Scheduled Task State", StringComparison.OrdinalIgnoreCase));

            if (taskNameIndex < 0 || scheduleTypeIndex < 0)
            {
                return Array.Empty<StartupApp>();
            }

            var apps = new List<StartupApp>();
            for (int i = 1; i < lines.Length; i++)
            {
                var row = ParseCsvLine(lines[i]);
                if (row.Count <= Math.Max(taskNameIndex, scheduleTypeIndex))
                {
                    continue;
                }

                string scheduleType = row[scheduleTypeIndex];
                if (!scheduleType.Contains("logon", StringComparison.OrdinalIgnoreCase) &&
                    !scheduleType.Contains("startup", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string taskName = row[taskNameIndex];
                string command = taskToRunIndex >= 0 && taskToRunIndex < row.Count ? row[taskToRunIndex] : "";
                string state = stateIndex >= 0 && stateIndex < row.Count && row[stateIndex].Contains("Disabled", StringComparison.OrdinalIgnoreCase)
                    ? "Disabled"
                    : "Enabled";

                apps.Add(new StartupApp
                {
                    Name = taskName.TrimStart('\\'),
                    Command = command,
                    Location = "Scheduled Task",
                    State = state,
                    Key = taskName,
                    EntryType = "ScheduledTask"
                });
            }

            return apps;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            values.Add(current.ToString());
            return values;
        }

        private static RegistryValueKind SafeValueKind(RegistryKey key, string valueName)
        {
            try
            {
                return key.GetValueKind(valueName);
            }
            catch
            {
                return RegistryValueKind.String;
            }
        }

        private static RegistryKey GetHive(string hiveName)
        {
            return hiveName == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
        }

        private static void SaveBackup(StartupBackupEntry backup)
        {
            var backups = LoadBackups();
            backups.Add(backup);
            SaveBackups(backups);
        }

        private static List<StartupBackupEntry> LoadBackups()
        {
            try
            {
                if (!File.Exists(AppPaths.StartupBackupPath))
                {
                    return new List<StartupBackupEntry>();
                }

                return JsonSerializer.Deserialize<List<StartupBackupEntry>>(File.ReadAllText(AppPaths.StartupBackupPath)) ?? new List<StartupBackupEntry>();
            }
            catch
            {
                return new List<StartupBackupEntry>();
            }
        }

        private static void SaveBackups(List<StartupBackupEntry> backups)
        {
            var json = JsonSerializer.Serialize(backups, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.StartupBackupPath, json);
        }

        private sealed record StartupRegistryLocation(RegistryKey Hive, string HiveName, string Path, string DisplayName, bool Disabled);
    }
}
