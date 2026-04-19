using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GregOriginSuiteApp.Services
{
    public sealed class DefragService
    {
        private readonly IProcessRunner _runner;

        public DefragService(IProcessRunner runner)
        {
            _runner = runner;
        }

        public IReadOnlyList<string> LoadFixedDrives()
        {
            return DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.Name.Substring(0, 2))
                .ToList();
        }

        public Task<CommandResult> AnalyzeAsync(string drive, CancellationToken cancellationToken = default)
        {
            return RunAsync(drive, "/A /V", cancellationToken);
        }

        public Task<CommandResult> OptimizeAsync(string drive, bool retrim, bool bootOptimize, CancellationToken cancellationToken = default)
        {
            string args = "/O /U /V";
            if (retrim) args += " /L";
            if (bootOptimize) args += " /B";
            return RunAsync(drive, args, cancellationToken);
        }

        private Task<CommandResult> RunAsync(string drive, string args, CancellationToken cancellationToken)
        {
            return _runner.RunAsync(new CommandSpec
            {
                FileName = "defrag.exe",
                Arguments = $"{drive} {args}",
                CaptureOutput = true
            }, cancellationToken);
        }
    }
}
