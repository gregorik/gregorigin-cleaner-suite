using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GregOriginSuiteApp.Models;

namespace GregOriginSuiteApp.Services
{
    public sealed class ServiceBackupEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string ServiceName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string PreviousStartMode { get; set; } = "";
        public bool Restored { get; set; }
    }

    public sealed class ServiceControlService
    {
        private static readonly IReadOnlyDictionary<string, ServiceRule> Allowlist =
            new Dictionary<string, ServiceRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["DiagTrack"] = new("Connected User Experiences and Telemetry", "Manual", "Telemetry collection; safe to review on most personal systems."),
                ["dmwappushservice"] = new("Device Management Wireless Application Protocol", "Manual", "Enterprise/device telemetry path; rarely needed on personal systems."),
                ["MapsBroker"] = new("Downloaded Maps Manager", "Manual", "Only needed for offline maps."),
                ["RetailDemo"] = new("Retail Demo Service", "Disabled", "Retail store demo mode."),
                ["RemoteRegistry"] = new("Remote Registry", "Disabled", "Remote registry edits; normally disabled on consumer machines."),
                ["SysMain"] = new("SysMain", "Automatic", "Prefetch/cache service; can be reviewed on low-memory or SSD-focused tuning."),
                ["WMPNetworkSvc"] = new("Windows Media Player Network Sharing", "Manual", "Legacy media sharing."),
                ["WSearch"] = new("Windows Search", "Automatic", "Indexing service; disabling affects search quality."),
                ["XblAuthManager"] = new("Xbox Live Auth Manager", "Manual", "Xbox services; only needed for Xbox apps/games."),
                ["XblGameSave"] = new("Xbox Live Game Save", "Manual", "Xbox cloud save support."),
                ["XboxGipSvc"] = new("Xbox Accessory Management Service", "Manual", "Xbox accessory support."),
                ["XboxNetApiSvc"] = new("Xbox Live Networking Service", "Manual", "Xbox networking support.")
            };

        private readonly IProcessRunner _runner;

        public ServiceControlService(IProcessRunner runner)
        {
            _runner = runner;
        }

        public Task<IReadOnlyList<ServiceApp>> LoadAllowlistedServicesAsync()
        {
            return Task.Run<IReadOnlyList<ServiceApp>>(() =>
            {
                var services = new List<ServiceApp>();
                using var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, State, StartMode FROM Win32_Service");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString() ?? "";
                    if (!Allowlist.TryGetValue(name, out ServiceRule? rule))
                    {
                        continue;
                    }

                    services.Add(new ServiceApp
                    {
                        Name = name,
                        DisplayName = obj["DisplayName"]?.ToString() ?? rule.DisplayName,
                        Status = obj["State"]?.ToString() ?? "",
                        StartupType = obj["StartMode"]?.ToString() ?? "",
                        SafeDefaultStartupType = rule.DefaultStartMode,
                        AllowlistReason = rule.Reason
                    });
                }

                return services.OrderBy(s => s.DisplayName).ToList();
            });
        }

        public async Task<OperationResult> ToggleAsync(IEnumerable<ServiceApp> services, CancellationToken cancellationToken = default)
        {
            var result = new OperationResult();
            foreach (var service in services)
            {
                if (!Allowlist.ContainsKey(service.Name))
                {
                    result.Failures.Add($"{service.Name}: not in curated allowlist.");
                    continue;
                }

                string action = service.Status.Equals("Running", StringComparison.OrdinalIgnoreCase) ? "stop" : "start";
                var command = new CommandSpec
                {
                    FileName = "net.exe",
                    Arguments = $"{action} {CommandLineParser.Quote(service.Name)}",
                    CaptureOutput = true
                };
                result.AddResult(await _runner.RunAsync(command, cancellationToken), $"{service.Name} {action}");
            }

            return result;
        }

        public async Task<OperationResult> DisableAsync(IEnumerable<ServiceApp> services, CancellationToken cancellationToken = default)
        {
            var result = new OperationResult();
            foreach (var service in services)
            {
                if (!Allowlist.ContainsKey(service.Name))
                {
                    result.Failures.Add($"{service.Name}: not in curated allowlist.");
                    continue;
                }

                SaveBackup(new ServiceBackupEntry
                {
                    ServiceName = service.Name,
                    DisplayName = service.DisplayName,
                    PreviousStartMode = service.StartupType
                });

                var command = new CommandSpec
                {
                    FileName = "sc.exe",
                    Arguments = $"config {CommandLineParser.Quote(service.Name)} start= disabled",
                    CaptureOutput = true
                };
                result.AddResult(await _runner.RunAsync(command, cancellationToken), $"{service.Name} disable");
            }

            return result;
        }

        public async Task<OperationResult> RestoreBackupsAsync(CancellationToken cancellationToken = default)
        {
            var result = new OperationResult();
            var backups = LoadBackups();
            foreach (var backup in backups.Where(b => !b.Restored).OrderByDescending(b => b.CreatedAt))
            {
                string scMode = ToScStartMode(backup.PreviousStartMode);
                var command = new CommandSpec
                {
                    FileName = "sc.exe",
                    Arguments = $"config {CommandLineParser.Quote(backup.ServiceName)} start= {scMode}",
                    CaptureOutput = true
                };

                var commandResult = await _runner.RunAsync(command, cancellationToken);
                result.AddResult(commandResult, $"{backup.ServiceName} restore startup mode {backup.PreviousStartMode}");
                if (commandResult.Success)
                {
                    backup.Restored = true;
                }
            }

            SaveBackups(backups);
            return result;
        }

        private static string ToScStartMode(string startMode)
        {
            return startMode.ToLowerInvariant() switch
            {
                "auto" or "automatic" => "auto",
                "manual" or "demand" => "demand",
                "disabled" => "disabled",
                _ => "demand"
            };
        }

        private static void SaveBackup(ServiceBackupEntry backup)
        {
            var backups = LoadBackups();
            backups.Add(backup);
            SaveBackups(backups);
        }

        private static List<ServiceBackupEntry> LoadBackups()
        {
            try
            {
                if (!File.Exists(AppPaths.ServiceBackupPath))
                {
                    return new List<ServiceBackupEntry>();
                }

                return JsonSerializer.Deserialize<List<ServiceBackupEntry>>(File.ReadAllText(AppPaths.ServiceBackupPath)) ?? new List<ServiceBackupEntry>();
            }
            catch
            {
                return new List<ServiceBackupEntry>();
            }
        }

        private static void SaveBackups(List<ServiceBackupEntry> backups)
        {
            string json = JsonSerializer.Serialize(backups, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.ServiceBackupPath, json);
        }

        private sealed record ServiceRule(string DisplayName, string DefaultStartMode, string Reason);
    }
}
