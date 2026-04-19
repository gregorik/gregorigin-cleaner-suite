using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GregOriginSuiteApp.Models;
using Microsoft.Win32;

namespace GregOriginSuiteApp.Services
{
    public sealed class InstalledAppService
    {
        public Task<IReadOnlyList<InstalledApp>> LoadInstalledAppsAsync()
        {
            return Task.Run<IReadOnlyList<InstalledApp>>(() =>
            {
                var apps = new List<InstalledApp>();
                LoadHive(apps, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                LoadHive(apps, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
                LoadHive(apps, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall");

                return apps
                    .GroupBy(a => new { a.DisplayName, a.UninstallString })
                    .Select(g => g.First())
                    .OrderBy(a => a.DisplayName)
                    .ToList();
            });
        }

        private static void LoadHive(List<InstalledApp> apps, RegistryKey hive, string keyPath)
        {
            using RegistryKey? key = hive.OpenSubKey(keyPath);
            if (key == null)
            {
                return;
            }

            foreach (string subkeyName in key.GetSubKeyNames())
            {
                using RegistryKey? subkey = key.OpenSubKey(subkeyName);
                if (subkey == null)
                {
                    continue;
                }

                var displayName = subkey.GetValue("DisplayName") as string;
                var uninstallString = subkey.GetValue("UninstallString") as string;
                int isSystemComponent = subkey.GetValue("SystemComponent") is int value ? value : 0;

                if (!string.IsNullOrWhiteSpace(displayName) &&
                    !string.IsNullOrWhiteSpace(uninstallString) &&
                    isSystemComponent != 1)
                {
                    apps.Add(new InstalledApp
                    {
                        DisplayName = displayName,
                        Publisher = subkey.GetValue("Publisher") as string ?? "",
                        DisplayVersion = subkey.GetValue("DisplayVersion") as string ?? "",
                        UninstallString = uninstallString
                    });
                }
            }
        }
    }
}
