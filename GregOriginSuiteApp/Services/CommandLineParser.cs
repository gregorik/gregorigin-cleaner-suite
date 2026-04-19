using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GregOriginSuiteApp.Services
{
    public static class CommandLineParser
    {
        public static IReadOnlyList<string> Split(string commandLine)
        {
            var parts = new List<string>();
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return parts;
            }

            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < commandLine.Length; i++)
            {
                char c = commandLine[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
            {
                parts.Add(current.ToString());
            }

            return parts;
        }

        public static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            if (!value.Any(char.IsWhiteSpace) && !value.Contains('"'))
            {
                return value;
            }

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
