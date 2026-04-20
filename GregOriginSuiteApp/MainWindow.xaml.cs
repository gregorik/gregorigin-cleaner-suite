using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GregOriginSuiteApp.Models;
using GregOriginSuiteApp.Services;

namespace GregOriginSuiteApp
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<StartupApp> StartupApps { get; } = new();
        public ObservableCollection<ServiceApp> ServiceApps { get; } = new();
        public ObservableCollection<SmartDrive> SmartDrives { get; } = new();

        private readonly IProcessRunner _runner = new ProcessRunner();
        private readonly StartupService _startupService = new();
        private readonly HardwareService _hardwareService = new();
        private readonly AppSettings _appSettings;
        private readonly CleanupService _cleanupService;
        private readonly ServiceControlService _serviceControlService;

        private bool _isDarkMode;
        private bool _isExitRequested;
        private CleanupPlan? _lastCleanupPlan;
        private CancellationTokenSource? _largeFileScanCts;

        public MainWindow()
        {
            InitializeComponent();

            _cleanupService = new CleanupService(_runner);
            _serviceControlService = new ServiceControlService(_runner);
            _appSettings = AppSettings.Load();

            StartupList.ItemsSource = StartupApps;
            ServicesList.ItemsSource = ServiceApps;
            SmartList.ItemsSource = SmartDrives;

            chkSkipConfirmations.IsChecked = _appSettings.SkipConfirmations;
            ApplyTheme(_appSettings.IsDarkMode);

            Loaded += async (_, _) =>
            {
                await LoadStartupAppsAsync();
                await LoadServicesAsync();
                await LoadHardwareTelemetryAsync();
            };
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExitRequested)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) => ShowFromTray();
        private void TrayIcon_Open_Click(object sender, RoutedEventArgs e) => ShowFromTray();

        private void TrayIcon_Exit_Click(object sender, RoutedEventArgs e)
        {
            _isExitRequested = true;
            TrayIcon.Dispose();
            Application.Current.Shutdown();
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ThemeToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!_isDarkMode);
            _appSettings.IsDarkMode = _isDarkMode;
            _appSettings.Save();
        }

        private void ApplyTheme(bool darkMode)
        {
            _isDarkMode = darkMode;
            if (_isDarkMode)
            {
                Resources["WindowBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181A"));
                Resources["TextForeground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
                Resources["ListBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#232325"));
                Resources["TextBoxBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30"));
                Resources["PrimaryColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6EB6FF"));
                Resources["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E3E42"));
                ThemeToggleBtn.Content = "Light Mode";
                return;
            }

            Resources["WindowBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F9FF"));
            Resources["TextForeground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
            Resources["ListBackground"] = new SolidColorBrush(Colors.White);
            Resources["TextBoxBackground"] = new SolidColorBrush(Colors.White);
            Resources["PrimaryColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#004E8C"));
            Resources["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ADD8E6"));
            ThemeToggleBtn.Content = "Dark Mode";
        }

        private void ChkSkipConfirmations_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (chkSkipConfirmations == null)
            {
                return;
            }

            _appSettings.SkipConfirmations = chkSkipConfirmations.IsChecked == true;
            _appSettings.Save();
        }

        private void SiteBtn_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://gregorigin.com") { UseShellExecute = true });
        }

        private void LogMsg(string msg)
        {
            LogBox.AppendText($"{msg}{Environment.NewLine}");
            LogBox.ScrollToEnd();
        }

        private CleanupOptions GetCleanupOptions()
        {
            return new CleanupOptions
            {
                WindowsTemp = chkWinTemp.IsChecked == true,
                UserTemp = chkUserTemp.IsChecked == true,
                EdgeCache = chkEdge.IsChecked == true,
                ChromeCache = chkChrome.IsChecked == true,
                BrowserSqlite = chkBrowserSqlite.IsChecked == true,
                Prefetch = chkPrefetch.IsChecked == true,
                SoftwareDistribution = chkSoftwareDist.IsChecked == true,
                SystemRestore = chkSystemRestore.IsChecked == true,
                RecycleBin = chkRecycle.IsChecked == true
            };
        }

        private async void ScanBtn_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Clear();
            CleanBtn.IsEnabled = false;
            try
            {
                _lastCleanupPlan = await _cleanupService.BuildPlanAsync(GetCleanupOptions());
                LogCleanupPlan(_lastCleanupPlan);
            }
            catch (Exception ex)
            {
                LogMsg("Analysis failed: " + ex.Message);
            }
            finally
            {
                CleanBtn.IsEnabled = true;
            }
        }

        private async void CleanBtn_Click(object sender, RoutedEventArgs e)
        {
            CleanupOptions options = GetCleanupOptions();
            if (CleanupNeedsAdmin(options) && !EnsureAdministrator("This cleanup selection modifies protected system locations."))
            {
                return;
            }

            LogMsg("Creating required dry-run audit before cleanup...");
            _lastCleanupPlan = await _cleanupService.BuildPlanAsync(options);
            LogCleanupPlan(_lastCleanupPlan);

            if (!_appSettings.SkipConfirmations)
            {
                string message = $"Delete {_lastCleanupPlan.Files.Count} files ({FormatBytes(_lastCleanupPlan.TotalBytes)}) and run {_lastCleanupPlan.SpecialActions.Count + _lastCleanupPlan.SqliteActions.Count} extra actions?\n\nAudit log:\n{_lastCleanupPlan.AuditPath}";
                if (MessageBox.Show(message, "Confirm cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    LogMsg("Cleanup cancelled by user.");
                    return;
                }
            }

            CleanupExecutionResult result;
            try
            {
                result = await _cleanupService.ExecutePlanAsync(_lastCleanupPlan);
            }
            catch (OperationCanceledException)
            {
                LogMsg("Cleanup cancelled.");
                return;
            }

            LogMsg($"Deleted {result.FilesDeleted} files, {result.DirectoriesDeleted} directories, {FormatBytes(result.BytesDeleted)}.");
            if (result.SqliteRowsDeleted > 0) LogMsg($"SQLite rows deleted: {result.SqliteRowsDeleted}.");
            foreach (var message in result.Messages) LogMsg(message);
            foreach (var failure in result.Failures) LogMsg("FAILED: " + failure);
            LogMsg($"Execution audit appended: {result.AuditPath}");
        }

        private void LogCleanupPlan(CleanupPlan plan)
        {
            LogMsg($"Dry-run complete: {plan.Files.Count} files, {plan.Directories.Count} directories, {FormatBytes(plan.TotalBytes)}.");
            foreach (var target in plan.Targets)
            {
                long bytes = plan.Files.Where(f => f.TargetName == target.Name).Sum(f => f.Bytes);
                int files = plan.Files.Count(f => f.TargetName == target.Name);
                LogMsg($"{target.Name}: {files} files, {FormatBytes(bytes)}.");
            }

            foreach (var sqlite in plan.SqliteActions)
            {
                string detail = string.IsNullOrWhiteSpace(sqlite.Failure)
                    ? $"{sqlite.Rows} rows queued from {sqlite.TableName}"
                    : $"not available: {sqlite.Failure}";
                LogMsg($"SQLite {Path.GetFileName(sqlite.DatabasePath)}: {detail}.");
            }

            foreach (var action in plan.SpecialActions) LogMsg("Special action: " + action);
            foreach (var failure in plan.Failures) LogMsg("PLAN WARNING: " + failure);
            LogMsg("Audit log: " + plan.AuditPath);
        }

        private async Task LoadStartupAppsAsync()
        {
            StartupStatus.Content = "Scanning startup items...";
            StartupApps.Clear();
            foreach (var app in await _startupService.LoadAsync())
            {
                StartupApps.Add(app);
            }

            StartupStatus.Content = $"Found {StartupApps.Count} startup items.";
        }

        private async void RefreshStartupBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadStartupAppsAsync();
        }

        private async void ToggleStartupBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = StartupList.SelectedItems.Cast<StartupApp>().ToList();
            if (selectedApps.Count == 0 || !EnsureAdministratorForStartup(selectedApps, "Changing machine-wide startup entries requires administrator rights."))
            {
                return;
            }

            OperationResult result = await _startupService.ToggleAsync(selectedApps);
            ShowOperationResult(result, StartupStatus, "Startup toggle complete.");
            await LoadStartupAppsAsync();
        }

        private async void DeleteStartupBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = StartupList.SelectedItems.Cast<StartupApp>().ToList();
            if (selectedApps.Count == 0 || !EnsureAdministratorForStartup(selectedApps, "Deleting machine-wide startup entries requires administrator rights."))
            {
                return;
            }

            if (!_appSettings.SkipConfirmations &&
                MessageBox.Show($"Delete {selectedApps.Count} startup items? Restorable backups will be written first.", "GregOrigin Suite", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            OperationResult result = await _startupService.DeleteAsync(selectedApps);
            ShowOperationResult(result, StartupStatus, "Startup delete complete.");
            await LoadStartupAppsAsync();
        }

        private async void RestoreStartupBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdministrator("Restoring startup backups can write to machine-wide startup locations."))
            {
                return;
            }

            OperationResult result = await _startupService.RestoreBackupsAsync();
            ShowOperationResult(result, StartupStatus, "Startup backups restored.");
            await LoadStartupAppsAsync();
        }

        private async Task LoadServicesAsync()
        {
            OptimizeStatus.Content = "Scanning curated service allowlist...";
            ServiceApps.Clear();
            try
            {
                foreach (var service in await _serviceControlService.LoadAllowlistedServicesAsync())
                {
                    ServiceApps.Add(service);
                }

                OptimizeStatus.Content = $"Found {ServiceApps.Count} allowlisted services.";
            }
            catch (Exception ex)
            {
                OptimizeStatus.Content = "Service scan failed: " + ex.Message;
            }
        }

        private async void RefreshServicesBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadServicesAsync();
        }

        private async void ToggleServiceBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = ServicesList.SelectedItems.Cast<ServiceApp>().ToList();
            if (selected.Count == 0 || !EnsureAdministrator("Starting or stopping services requires administrator rights."))
            {
                return;
            }

            OperationResult result = await _serviceControlService.ToggleAsync(selected);
            ShowOperationResult(result, OptimizeStatus, "Service toggle complete.");
            await LoadServicesAsync();
        }

        private async void DisableServiceBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = ServicesList.SelectedItems.Cast<ServiceApp>().ToList();
            if (selected.Count == 0 || !EnsureAdministrator("Disabling services requires administrator rights."))
            {
                return;
            }

            if (!_appSettings.SkipConfirmations &&
                MessageBox.Show($"Disable {selected.Count} allowlisted services? Current startup modes will be backed up first.", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            OperationResult result = await _serviceControlService.DisableAsync(selected);
            ShowOperationResult(result, OptimizeStatus, "Service disable complete.");
            await LoadServicesAsync();
        }

        private async void RestoreServicesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdministrator("Restoring service startup modes requires administrator rights."))
            {
                return;
            }

            OperationResult result = await _serviceControlService.RestoreBackupsAsync();
            ShowOperationResult(result, OptimizeStatus, "Service backups restored.");
            await LoadServicesAsync();
        }

        private async void NetworkFlushBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdministrator("Resetting DNS and Winsock requires administrator rights."))
            {
                return;
            }

            OptimizeStatus.Content = "Flushing DNS and resetting Winsock...";
            var result = new OperationResult();
            result.AddResult(await _runner.RunAsync(new CommandSpec { FileName = "ipconfig.exe", Arguments = "/flushdns", CaptureOutput = true }), "DNS flush");
            result.AddResult(await _runner.RunAsync(new CommandSpec { FileName = "netsh.exe", Arguments = "winsock reset", CaptureOutput = true }), "Winsock reset");
            ShowOperationResult(result, OptimizeStatus, "Network flush complete. Restart may be required.");
        }

        private async Task LoadHardwareTelemetryAsync()
        {
            try
            {
                HardwareSnapshot snapshot = await _hardwareService.LoadSnapshotAsync();
                TxtCpu.Text = $"CPU: {snapshot.Cpu}";
                TxtRam.Text = $"RAM: {snapshot.Ram}";
                TxtGpu.Text = $"GPU: {snapshot.Gpu}";
                TxtOs.Text = $"OS: {snapshot.Os}";
                SmartDrives.Clear();
                foreach (var drive in snapshot.Drives) SmartDrives.Add(drive);
            }
            catch (Exception ex)
            {
                TxtCpu.Text = "Hardware scan failed: " + ex.Message;
            }
        }

        private async void RefreshHardwareBtn_Click(object sender, RoutedEventArgs e)
        {
            TxtCpu.Text = "CPU: Scanning...";
            TxtRam.Text = "RAM: Scanning...";
            TxtGpu.Text = "GPU: Scanning...";
            TxtOs.Text = "OS: Scanning...";
            await LoadHardwareTelemetryAsync();
        }

        private async void ScanLargeFilesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_largeFileScanCts != null)
            {
                _largeFileScanCts.Cancel();
                return;
            }

            _largeFileScanCts = new CancellationTokenSource();
            ScanLargeFilesBtn.Content = "Cancel Large File Scan";
            ScanLargeFilesBtn.IsEnabled = true;

            try
            {
                LargeFileScanResult result = await _hardwareService.ScanLargeFilesAsync(@"C:\", 50, _largeFileScanCts.Token);
                if (result.Cancelled)
                {
                    MessageBox.Show("Large file scan cancelled.", "Hardware", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (result.Files.Count > 0)
                {
                    string msg = "Top 50 Largest Files on C:\\" + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, result.Files);
                    string path = Path.Combine(Path.GetTempPath(), "LargeFiles.txt");
                    File.WriteAllText(path, msg);
                    Process.Start(new ProcessStartInfo("notepad.exe", path) { UseShellExecute = true });
                }
            }
            finally
            {
                _largeFileScanCts.Dispose();
                _largeFileScanCts = null;
                ScanLargeFilesBtn.Content = "Scan C:\\ for Top 50 Large Files";
            }
        }

        private bool EnsureAdministratorForStartup(IReadOnlyList<StartupApp> apps, string reason)
        {
            bool needsAdmin = apps.Any(app =>
                app.RegistryHive == "HKLM" ||
                app.Location.Contains("All Users", StringComparison.OrdinalIgnoreCase));
            return !needsAdmin || EnsureAdministrator(reason);
        }

        private bool EnsureAdministrator(string reason)
        {
            if (AdminHelper.IsRunningAsAdministrator())
            {
                return true;
            }

            if (MessageBox.Show($"{reason}\n\nRestart GregOrigin Cleaner Suite as administrator now?", "Administrator required", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return false;
            }

            try
            {
                AdminHelper.RestartElevated();
                _isExitRequested = true;
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to restart elevated: " + ex.Message, "Elevation failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return false;
        }

        private static bool CleanupNeedsAdmin(CleanupOptions options)
        {
            return options.WindowsTemp ||
                   options.Prefetch ||
                   options.SoftwareDistribution ||
                   options.SystemRestore ||
                   options.RecycleBin;
        }

        private static string FormatBytes(long bytes)
        {
            return $"{Math.Round(bytes / 1048576.0, 2)} MB";
        }

        private static string DescribeCommandFailure(CommandResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.Error)) return result.Error;
            if (result.Cancelled) return "Command cancelled.";
            string stderr = string.IsNullOrWhiteSpace(result.StandardError) ? "" : $"{Environment.NewLine}{result.StandardError.Trim()}";
            return $"{result.FileName} exited with code {(result.ExitCode?.ToString() ?? "unknown")}.{stderr}";
        }

        private static void ShowOperationResult(OperationResult result, Label status, string successMessage)
        {
            status.Content = result.Success ? successMessage : $"{result.Failures.Count} failures. See dialog.";
            if (!result.Success)
            {
                MessageBox.Show(string.Join(Environment.NewLine, result.Failures), "Operation failures", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}