using System;
using System.Collections.Generic;
using System.Linq;
using GregOriginSuiteApp.Models;

namespace GregOriginSuiteApp.Services
{
    public static class WingetParser
    {
        public static IReadOnlyList<UpdateApp> ParseUpgradeOutput(string output)
        {
            return ParseTable(output, isSearch: false);
        }

        public static IReadOnlyList<UpdateApp> ParseSearchOutput(string output)
        {
            return ParseTable(output, isSearch: true);
        }

        public static CommandSpec BuildUpgradeCommand(UpdateApp app)
        {
            return new CommandSpec
            {
                FileName = "winget.exe",
                Arguments = $"upgrade --id {CommandLineParser.Quote(app.Id)} --exact --accept-source-agreements --accept-package-agreements",
                CaptureOutput = false
            };
        }

        public static CommandSpec BuildInstallCommand(UpdateApp app)
        {
            return new CommandSpec
            {
                FileName = "winget.exe",
                Arguments = $"install --id {CommandLineParser.Quote(app.Id)} --exact --accept-source-agreements --accept-package-agreements",
                CaptureOutput = false
            };
        }

        public static CommandSpec BuildUpgradeAllCommand()
        {
            return new CommandSpec
            {
                FileName = "winget.exe",
                Arguments = "upgrade --all --include-unknown --accept-source-agreements --accept-package-agreements",
                CaptureOutput = false
            };
        }

        private static IReadOnlyList<UpdateApp> ParseTable(string output, bool isSearch)
        {
            var apps = new List<UpdateApp>();
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int headerIndex = Array.FindIndex(lines, line => line.TrimStart().StartsWith("Name", StringComparison.OrdinalIgnoreCase) && line.Contains(" Id "));
            if (headerIndex < 0)
            {
                return apps;
            }

            string header = lines[headerIndex];
            int idIndex = header.IndexOf("Id", StringComparison.Ordinal);
            int versionIndex = header.IndexOf("Version", StringComparison.Ordinal);
            int availableIndex = header.IndexOf("Available", StringComparison.Ordinal);
            int sourceIndex = header.IndexOf("Source", StringComparison.Ordinal);
            if (idIndex < 0 || versionIndex < 0)
            {
                return apps;
            }

            for (int i = headerIndex + 1; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("-"))
                {
                    continue;
                }

                string name = Slice(line, 0, idIndex).Trim();
                string id = Slice(line, idIndex, versionIndex).Trim();
                string version = availableIndex > versionIndex
                    ? Slice(line, versionIndex, availableIndex).Trim()
                    : sourceIndex > versionIndex
                        ? Slice(line, versionIndex, sourceIndex).Trim()
                        : Slice(line, versionIndex, line.Length).Trim();
                string available = availableIndex >= 0
                    ? sourceIndex > availableIndex ? Slice(line, availableIndex, sourceIndex).Trim() : Slice(line, availableIndex, line.Length).Trim()
                    : "";
                string source = sourceIndex >= 0 ? Slice(line, sourceIndex, line.Length).Trim() : "winget";

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                apps.Add(new UpdateApp
                {
                    Name = name,
                    Id = id,
                    Current = isSearch ? "-" : version,
                    Available = isSearch ? version : available,
                    Source = isSearch ? "Search Result" : source
                });
            }

            return apps;
        }

        private static string Slice(string value, int start, int end)
        {
            if (start >= value.Length)
            {
                return "";
            }

            int length = Math.Max(0, Math.Min(end, value.Length) - start);
            return value.Substring(start, length);
        }
    }
}
