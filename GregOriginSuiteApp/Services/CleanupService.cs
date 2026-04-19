using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace GregOriginSuiteApp.Services
{
    public sealed class CleanupService
    {
        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private const uint SherbNoConfirmation = 1;
        private const uint SherbNoProgressUi = 2;
        private const uint SherbNoSound = 4;

        private readonly IProcessRunner _runner;

        public CleanupService(IProcessRunner runner)
        {
            _runner = runner;
        }

        public IReadOnlyList<CleanupTarget> GetTargets(CleanupOptions options)
        {
            var targets = new List<CleanupTarget>();
            if (options.WindowsTemp) targets.Add(new CleanupTarget { Name = "Windows Temp", Path = @"C:\Windows\Temp" });
            if (options.UserTemp) targets.Add(new CleanupTarget { Name = "User Temp", Path = Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath() });
            if (options.EdgeCache) targets.Add(new CleanupTarget { Name = "Edge Cache", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data\Default\Cache\Cache_Data") });
            if (options.ChromeCache) targets.Add(new CleanupTarget { Name = "Chrome Cache", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data\Default\Cache\Cache_Data") });
            if (options.Prefetch) targets.Add(new CleanupTarget { Name = "Windows Prefetch", Path = @"C:\Windows\Prefetch" });
            if (options.SoftwareDistribution) targets.Add(new CleanupTarget { Name = "WinUpdate Cache", Path = @"C:\Windows\SoftwareDistribution\Download" });
            return targets;
        }

        public Task<CleanupPlan> BuildPlanAsync(CleanupOptions options, CancellationToken cancellationToken = default)
        {
            return BuildPlanForTargetsAsync(GetTargets(options), options.BrowserSqlite, options.SystemRestore, options.RecycleBin, cancellationToken);
        }

        public Task<CleanupPlan> BuildPlanForTargetsAsync(
            IEnumerable<CleanupTarget> targets,
            bool includeBrowserSqlite = false,
            bool includeSystemRestore = false,
            bool includeRecycleBin = false,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var plan = new CleanupPlan();
                var enumOptions = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    ReturnSpecialDirectories = false
                };

                foreach (var target in targets)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    plan.Targets.Add(target);
                    if (!Directory.Exists(target.Path))
                    {
                        plan.Failures.Add($"{target.Name}: path not found ({target.Path}).");
                        continue;
                    }

                    try
                    {
                        var root = new DirectoryInfo(target.Path);
                        foreach (var file in root.EnumerateFiles("*", enumOptions))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            plan.Files.Add(new CleanupFileItem
                            {
                                TargetName = target.Name,
                                Path = file.FullName,
                                Bytes = SafeLength(file)
                            });
                        }

                        foreach (var dir in root.EnumerateDirectories("*", enumOptions))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            plan.Directories.Add(new CleanupDirectoryItem
                            {
                                TargetName = target.Name,
                                Path = dir.FullName
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        plan.Failures.Add($"{target.Name}: failed to enumerate {target.Path}: {ex.Message}");
                    }
                }

                if (includeBrowserSqlite)
                {
                    AddBrowserSqliteActions(plan);
                }

                if (includeSystemRestore)
                {
                    plan.SpecialActions.Add("Delete all system restore shadow copies using vssadmin.exe.");
                }

                if (includeRecycleBin)
                {
                    plan.SpecialActions.Add("Empty Recycle Bin using SHEmptyRecycleBin.");
                }

                plan.Directories.Sort((a, b) => b.Path.Length.CompareTo(a.Path.Length));
                plan.AuditPath = WritePlanAudit(plan);
                return plan;
            }, cancellationToken);
        }

        public async Task<CleanupExecutionResult> ExecutePlanAsync(CleanupPlan plan, CancellationToken cancellationToken = default)
        {
            var result = new CleanupExecutionResult { AuditPath = plan.AuditPath };
            foreach (var file in plan.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file.Path);
                    if (info.Exists)
                    {
                        long bytes = info.Length;
                        info.Delete();
                        result.FilesDeleted++;
                        result.BytesDeleted += bytes;
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"Failed to delete file {file.Path}: {ex.Message}");
                }
            }

            foreach (var dir in plan.Directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (Directory.Exists(dir.Path) && !Directory.EnumerateFileSystemEntries(dir.Path).Any())
                    {
                        Directory.Delete(dir.Path, recursive: false);
                        result.DirectoriesDeleted++;
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"Failed to delete directory {dir.Path}: {ex.Message}");
                }
            }

            foreach (var action in plan.SqliteActions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(action.Failure))
                {
                    result.Failures.Add($"Skipped SQLite cleanup for {action.DatabasePath}: {action.Failure}");
                    continue;
                }

                try
                {
                    using var connection = new SqliteConnection($"Data Source={action.DatabasePath}");
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = $"DELETE FROM {action.TableName}";
                    result.SqliteRowsDeleted += command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"Failed to scrub SQLite table {action.TableName} in {action.DatabasePath}: {ex.Message}");
                }
            }

            if (plan.SpecialActions.Any(action => action.Contains("vssadmin.exe", StringComparison.OrdinalIgnoreCase)))
            {
                var command = new CommandSpec
                {
                    FileName = "vssadmin.exe",
                    Arguments = "Delete Shadows /All /Quiet",
                    CaptureOutput = true
                };
                CommandResult commandResult = await _runner.RunAsync(command, cancellationToken);
                AddCommandResult(result, commandResult, "System restore shadow copy purge");
            }

            if (plan.SpecialActions.Any(action => action.Contains("Recycle Bin", StringComparison.OrdinalIgnoreCase)))
            {
                uint code = SHEmptyRecycleBin(IntPtr.Zero, null, SherbNoConfirmation | SherbNoProgressUi | SherbNoSound);
                if (code == 0)
                {
                    result.Messages.Add("Recycle Bin emptied.");
                }
                else
                {
                    result.Failures.Add($"Recycle Bin empty failed with Shell32 code {code}.");
                }
            }

            AppendExecutionAudit(plan, result);
            return result;
        }

        private static long SafeLength(FileInfo file)
        {
            try
            {
                return file.Length;
            }
            catch
            {
                return 0;
            }
        }

        private static void AddBrowserSqliteActions(CleanupPlan plan)
        {
            var sqlitePaths = new[]
            {
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data\Default\Network\Cookies"), "cookies"),
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data\Default\History"), "urls"),
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data\Default\Network\Cookies"), "cookies"),
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data\Default\History"), "urls")
            };

            foreach (var (db, table) in sqlitePaths)
            {
                if (!File.Exists(db))
                {
                    plan.SqliteActions.Add(new CleanupSqliteAction
                    {
                        DatabasePath = db,
                        TableName = table,
                        Failure = "database not found"
                    });
                    continue;
                }

                try
                {
                    using var connection = new SqliteConnection($"Data Source={db};Mode=ReadOnly");
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = $"SELECT COUNT(*) FROM {table}";
                    long rows = Convert.ToInt64(command.ExecuteScalar());
                    plan.SqliteActions.Add(new CleanupSqliteAction
                    {
                        DatabasePath = db,
                        TableName = table,
                        Rows = rows
                    });
                }
                catch (Exception ex)
                {
                    plan.SqliteActions.Add(new CleanupSqliteAction
                    {
                        DatabasePath = db,
                        TableName = table,
                        Failure = ex.Message
                    });
                }
            }
        }

        private static string WritePlanAudit(CleanupPlan plan)
        {
            string fileName = $"cleanup-dry-run-{DateTime.Now:yyyyMMdd-HHmmss}.log";
            string path = Path.Combine(AppPaths.AuditDirectory, fileName);
            var sb = new StringBuilder();
            sb.AppendLine("GregOrigin Cleaner Suite cleanup dry-run audit");
            sb.AppendLine($"Created: {plan.CreatedAt:O}");
            sb.AppendLine($"Targets: {plan.Targets.Count}");
            sb.AppendLine($"Files: {plan.Files.Count}");
            sb.AppendLine($"Bytes: {plan.TotalBytes}");
            sb.AppendLine($"Directories: {plan.Directories.Count}");
            sb.AppendLine();

            foreach (var file in plan.Files)
            {
                sb.AppendLine($"FILE\t{file.Bytes}\t{file.TargetName}\t{file.Path}");
            }

            foreach (var dir in plan.Directories)
            {
                sb.AppendLine($"DIR\t{dir.TargetName}\t{dir.Path}");
            }

            foreach (var sqlite in plan.SqliteActions)
            {
                sb.AppendLine($"SQLITE\t{sqlite.Rows}\t{sqlite.TableName}\t{sqlite.DatabasePath}\t{sqlite.Failure}");
            }

            foreach (var action in plan.SpecialActions)
            {
                sb.AppendLine($"SPECIAL\t{action}");
            }

            foreach (var failure in plan.Failures)
            {
                sb.AppendLine($"PLAN-FAILURE\t{failure}");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }

        private static void AppendExecutionAudit(CleanupPlan plan, CleanupExecutionResult result)
        {
            if (string.IsNullOrWhiteSpace(plan.AuditPath))
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Execution audit");
            sb.AppendLine($"Executed: {DateTime.Now:O}");
            sb.AppendLine($"Files deleted: {result.FilesDeleted}");
            sb.AppendLine($"Directories deleted: {result.DirectoriesDeleted}");
            sb.AppendLine($"Bytes deleted: {result.BytesDeleted}");
            sb.AppendLine($"SQLite rows deleted: {result.SqliteRowsDeleted}");

            foreach (var message in result.Messages)
            {
                sb.AppendLine($"MESSAGE\t{message}");
            }

            foreach (var failure in result.Failures)
            {
                sb.AppendLine($"FAILURE\t{failure}");
            }

            File.AppendAllText(plan.AuditPath, sb.ToString(), Encoding.UTF8);
        }

        private static void AddCommandResult(CleanupExecutionResult result, CommandResult commandResult, string actionName)
        {
            if (commandResult.Success)
            {
                result.Messages.Add($"{actionName}: exit code 0.");
                return;
            }

            string detail = !string.IsNullOrWhiteSpace(commandResult.Error)
                ? commandResult.Error
                : $"exit code {(commandResult.ExitCode?.ToString() ?? "unknown")}; stderr: {commandResult.StandardError.Trim()}";
            result.Failures.Add($"{actionName} failed: {detail}");
        }
    }
}
