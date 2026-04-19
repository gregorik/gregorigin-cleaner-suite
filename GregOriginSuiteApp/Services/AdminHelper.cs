using System;
using System.Diagnostics;
using System.Security.Principal;

namespace GregOriginSuiteApp.Services
{
    public static class AdminHelper
    {
        public static bool IsRunningAsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void RestartElevated()
        {
            string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrWhiteSpace(exePath))
            {
                throw new InvalidOperationException("Unable to resolve application path for elevation.");
            }

            Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas"
            });
        }
    }
}
