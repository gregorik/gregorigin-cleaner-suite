using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GregOriginSuiteApp.Models;

namespace GregOriginSuiteApp.Services
{
    public sealed class WingetService
    {
        private readonly IProcessRunner _runner;

        public WingetService(IProcessRunner runner)
        {
            _runner = runner;
        }

        public async Task<(IReadOnlyList<UpdateApp> Apps, CommandResult Result)> CheckUpdatesAsync(CancellationToken cancellationToken = default)
        {
            var result = await _runner.RunAsync(new CommandSpec
            {
                FileName = "winget.exe",
                Arguments = "upgrade --accept-source-agreements",
                CaptureOutput = true
            }, cancellationToken);

            return (WingetParser.ParseUpgradeOutput(result.StandardOutput), result);
        }

        public async Task<(IReadOnlyList<UpdateApp> Apps, CommandResult Result)> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            var result = await _runner.RunAsync(new CommandSpec
            {
                FileName = "winget.exe",
                Arguments = $"search {CommandLineParser.Quote(query)} --accept-source-agreements",
                CaptureOutput = true
            }, cancellationToken);

            return (WingetParser.ParseSearchOutput(result.StandardOutput), result);
        }
    }
}
