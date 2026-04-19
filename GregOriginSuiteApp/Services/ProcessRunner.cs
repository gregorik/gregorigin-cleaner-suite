using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GregOriginSuiteApp.Services
{
    public interface IProcessRunner
    {
        Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default);
    }

    public sealed class ProcessRunner : IProcessRunner
    {
        public async Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
        {
            bool shellExecute = spec.UseShellExecute || spec.RunElevated || !spec.CaptureOutput;
            var startInfo = new ProcessStartInfo
            {
                FileName = spec.FileName,
                Arguments = spec.Arguments,
                UseShellExecute = shellExecute,
                CreateNoWindow = !shellExecute,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            if (spec.RunElevated)
            {
                startInfo.Verb = "runas";
            }

            if (!shellExecute && spec.CaptureOutput)
            {
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.StandardOutputEncoding = Encoding.UTF8;
                startInfo.StandardErrorEncoding = Encoding.UTF8;
            }

            using var process = new Process { StartInfo = startInfo };

            try
            {
                if (!process.Start())
                {
                    return new CommandResult
                    {
                        FileName = spec.FileName,
                        Arguments = spec.Arguments,
                        Started = false,
                        Error = "Process did not start."
                    };
                }

                Task<string> outputTask = Task.FromResult("");
                Task<string> errorTask = Task.FromResult("");
                if (!shellExecute && spec.CaptureOutput)
                {
                    outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                    errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
                }

                await process.WaitForExitAsync(cancellationToken);

                return new CommandResult
                {
                    FileName = spec.FileName,
                    Arguments = spec.Arguments,
                    Started = true,
                    ExitCode = process.ExitCode,
                    StandardOutput = await outputTask,
                    StandardError = await errorTask
                };
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return new CommandResult
                {
                    FileName = spec.FileName,
                    Arguments = spec.Arguments,
                    Started = true,
                    Cancelled = true,
                    Error = "Operation cancelled."
                };
            }
            catch (Exception ex)
            {
                return new CommandResult
                {
                    FileName = spec.FileName,
                    Arguments = spec.Arguments,
                    Started = false,
                    Error = ex.Message
                };
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }
    }
}
