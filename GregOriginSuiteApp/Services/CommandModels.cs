using System.Collections.Generic;

namespace GregOriginSuiteApp.Services
{
    public sealed class CommandSpec
    {
        public string FileName { get; init; } = "";
        public string Arguments { get; init; } = "";
        public bool UseShellExecute { get; init; }
        public bool RunElevated { get; init; }
        public bool CaptureOutput { get; init; } = true;

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Arguments) ? FileName : $"{FileName} {Arguments}";
        }
    }

    public sealed class CommandResult
    {
        public string FileName { get; init; } = "";
        public string Arguments { get; init; } = "";
        public int? ExitCode { get; init; }
        public string StandardOutput { get; init; } = "";
        public string StandardError { get; init; } = "";
        public bool Started { get; init; }
        public bool Cancelled { get; init; }
        public string Error { get; init; } = "";
        public bool Success => Started && !Cancelled && ExitCode == 0 && string.IsNullOrWhiteSpace(Error);
    }

    public sealed class OperationResult
    {
        public bool Success => Failures.Count == 0;
        public List<string> Messages { get; } = new();
        public List<string> Failures { get; } = new();

        public void AddResult(CommandResult result, string action)
        {
            if (result.Success)
            {
                Messages.Add($"{action}: exit code 0.");
                return;
            }

            string detail = result.Cancelled
                ? "cancelled"
                : !string.IsNullOrWhiteSpace(result.Error)
                    ? result.Error
                    : $"exit code {(result.ExitCode?.ToString() ?? "unknown")}";
            Failures.Add($"{action}: {detail}.");
        }
    }
}
