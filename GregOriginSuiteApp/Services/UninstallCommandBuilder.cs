using System;
using System.Linq;
using System.Text.RegularExpressions;
using GregOriginSuiteApp.Models;

namespace GregOriginSuiteApp.Services
{
    public static class UninstallCommandBuilder
    {
        private static readonly Regex ProductCodeRegex = new(@"\{[0-9A-Fa-f]{8}-([0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}\}", RegexOptions.Compiled);

        public static CommandSpec Build(InstalledApp app)
        {
            return Build(app.UninstallString);
        }

        public static CommandSpec Build(string uninstallString)
        {
            if (string.IsNullOrWhiteSpace(uninstallString))
            {
                throw new ArgumentException("Uninstall string is empty.", nameof(uninstallString));
            }

            var parts = CommandLineParser.Split(uninstallString);
            if (parts.Count == 0)
            {
                throw new ArgumentException("Uninstall string is empty.", nameof(uninstallString));
            }

            string fileName = parts[0];
            string args = string.Join(" ", parts.Skip(1).Select(CommandLineParser.Quote));

            if (fileName.EndsWith("msiexec.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "msiexec", StringComparison.OrdinalIgnoreCase))
            {
                args = BuildMsiArguments(args);
                return new CommandSpec
                {
                    FileName = "msiexec.exe",
                    Arguments = args,
                    CaptureOutput = false
                };
            }

            return new CommandSpec
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = true,
                CaptureOutput = false
            };
        }

        private static string BuildMsiArguments(string args)
        {
            string normalized = Regex.Replace(args, @"(?i)(^|\s)/(i|package)(?=\s|\{)", "$1/X");
            normalized = Regex.Replace(normalized, @"(?i)(^|\s)/(i|package)(?=\s)", "$1/X");

            if (!Regex.IsMatch(normalized, @"(?i)(^|\s)/(x|uninstall)(?=\s|\{)") && ProductCodeRegex.IsMatch(normalized))
            {
                normalized = "/X " + ProductCodeRegex.Match(normalized).Value;
            }

            if (!Regex.IsMatch(normalized, @"(?i)(^|\s)/(q|quiet|passive|qb|qn)(?=\s|$)"))
            {
                normalized += " /qb";
            }

            return normalized.Trim();
        }
    }
}
