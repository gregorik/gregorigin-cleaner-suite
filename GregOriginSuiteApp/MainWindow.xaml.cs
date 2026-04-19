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
        public ObservableCollection<InstalledApp> InstalledApps { get; } = new();
        public ObservableCollection<UpdateApp> UpdateApps { get; } = new();
        public ObservableCollection<StartupApp> StartupApps { get; } = new();
        public ObservableCollection<ServiceApp> ServiceApps { get; } = new();
        public ObservableCollection<SmartDrive> SmartDrives { get; } = new();

        private readonly IProcessRunner _runner = new ProcessRunner();
        private readonly InstalledAppService _installedAppService = new();
        private readonly StartupService _startupService = new();
        private readonly HardwareService _hardwareService = new();
        private readonly AppSettings _appSettings;
        private readonly CleanupService _cleanupService;
        private readonly WingetService _wingetService;
        private readonly ServiceControlService _serviceControlService;
        private readonly DefragService _defragService;

        private List<InstalledApp> _allInstalledApps = new();
        private bool _isDarkMode;
        private bool _isExitRequested;
        private CleanupPlan? _lastCleanupPlan;
        private CancellationTokenSource? _largeFileScanCts;
        private CancellationTokenSource? _defragCts;

        public MainWindow()
        {
            InitializeComponent();

            _cleanupService = new CleanupService(_runner);
            _wingetService = new WingetService(_runner);
            _serviceControlService = new ServiceControlService(_runner);
            _defragService = new DefragService(_runner);
            _appSettings = AppSettings.Load();

            AppList.ItemsSource = InstalledApps;
            UpdateList.ItemsSource = UpdateApps;
            StartupList.ItemsSource = StartupApps;
            ServicesList.ItemsSource = ServiceApps;
            SmartList.ItemsSource = SmartDrives;

            chkSkipConfirmations.IsChecked = _appSettings.SkipConfirmations;
            ApplyTheme(_appSettings.IsDarkMode);

            Loaded += async (_, _) =>
            {
                LoadDefragDrives();
                await LoadInstalledAppsAsync();
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

        private async Task LoadInstalledAppsAsync()
        {
            StatusLabel.Content = "Scanning registry...";
            InstalledApps.Clear();
            _allInstalledApps = (await _installedAppService.LoadInstalledAppsAsync()).ToList();
            foreach (var app in _allInstalledApps)
            {
                InstalledApps.Add(app);
            }

            StatusLabel.Content = $"Found {InstalledApps.Count} applications.";
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadInstalledAppsAsync();
        }

        private async void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = AppList.SelectedItems.Cast<InstalledApp>().ToList();
            if (selectedApps.Count == 0 || !EnsureAdministrator("Uninstalling applications requires administrator rights."))
            {
                return;
            }

            if (!_appSettings.SkipConfirmations &&
                MessageBox.Show($"Uninstall {selectedApps.Count} apps?", "GregOrigin Suite", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            foreach (var app in selectedApps)
            {
                StatusLabel.Content = $"Removing: {app.DisplayName}";
                CommandResult result;
                try
                {
                    result = await _runner.RunAsync(UninstallCommandBuilder.Build(app));
                }
                catch (Exception ex)
                {
                    StatusLabel.Content = $"{app.DisplayName}: command build failed.";
                    MessageBox.Show($"{app.DisplayName}: {ex.Message}", "Uninstall failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    continue;
                }

                if (!result.Success)
                {
                    MessageBox.Show(DescribeCommandFailure(result), $"Uninstall failed: {app.DisplayName}", MessageBoxButton.OK, MessageBoxImage.Error);
                    continue;
                }

                if (chkForceScrub.IsChecked == true)
                {
                    await ScrubAppRemnantsAsync(app);
                }
            }

            await LoadInstalledAppsAsync();
        }

        private async Task ScrubAppRemnantsAsync(InstalledApp app)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string cleanName = new(app.DisplayName.Where(char.IsLetterOrDigit).ToArray());
            string cleanPub = new(app.Publisher.Where(char.IsLetterOrDigit).ToArray());
            var targets = new List<CleanupTarget>
            {
                new() { Name = $"{app.DisplayName} LocalAppData", Path = Path.Combine(localAppData, cleanName) },
                new() { Name = $"{app.DisplayName} AppData", Path = Path.Combine(appData, cleanName) }
            };

            if (!string.IsNullOrWhiteSpace(cleanPub))
            {
                targets.Add(new CleanupTarget { Name = $"{app.DisplayName} Publisher LocalAppData", Path = Path.Combine(localAppData, cleanPub, cleanName) });
                targets.Add(new CleanupTarget { Name = $"{app.DisplayName} Publisher AppData", Path = Path.Combine(appData, cleanPub, cleanName) });
            }

            CleanupPlan plan = await _cleanupService.BuildPlanForTargetsAsync(targets);
            CleanupExecutionResult result = await _cleanupService.ExecutePlanAsync(plan);
            StatusLabel.Content = result.Success
                ? $"Removed remnants for {app.DisplayName}. Audit: {plan.AuditPath}"
                : $"Remnant scrub had failures. Audit: {plan.AuditPath}";
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == "Search installed apps...") SearchBox.Text = "";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchBox.Text == "Search installed apps...") return;
            string query = SearchBox.Text.ToLowerInvariant();
            InstalledApps.Clear();
            foreach (var app in _allInstalledApps.Where(a => a.DisplayName.ToLowerInvariant().Contains(query)))
            {
                InstalledApps.Add(app);
            }
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

        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus.Content = "Contacting Winget servers...";
            UpdateApps.Clear();
            CheckUpdateBtn.IsEnabled = false;

            var (apps, result) = await _wingetService.CheckUpdatesAsync();
            foreach (var app in apps) UpdateApps.Add(app);

            UpdateStatus.Content = result.Success
                ? $"Found {UpdateApps.Count} updates."
                : $"Winget failed: {DescribeCommandFailure(result)}";
            CheckUpdateBtn.IsEnabled = true;
        }

        private async void UpdateAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (UpdateApps.Count == 0) return;
            if (!_appSettings.SkipConfirmations &&
                MessageBox.Show("Launch Winget to update all packages?", "GregOrigin Suite", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            CommandResult result = await _runner.RunAsync(WingetParser.BuildUpgradeAllCommand());
            if (!result.Success) MessageBox.Show(DescribeCommandFailure(result), "Winget update failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void UpdateSelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = UpdateList.SelectedItems.Cast<UpdateApp>().Where(a => !string.IsNullOrWhiteSpace(a.Id)).ToList();
            if (selectedApps.Count == 0) return;

            if (!_appSettings.SkipConfirmations &&
                MessageBox.Show($"Launch Winget for {selectedApps.Count} selected package IDs?", "GregOrigin Suite", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            foreach (var app in selectedApps)
            {
                CommandSpec spec = app.Source == "Search Result" ? WingetParser.BuildInstallCommand(app) : WingetParser.BuildUpgradeCommand(app);
                CommandResult result = await _runner.RunAsync(spec);
                if (!result.Success)
                {
                    MessageBox.Show(DescribeCommandFailure(result), $"Winget failed: {app.Id}", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void WingetSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            string query = WingetSearchBox.Text;
            if (string.IsNullOrWhiteSpace(query) || query == "Search apps to install...") return;

            UpdateStatus.Content = "Searching Winget repository...";
            UpdateApps.Clear();
            WingetSearchBtn.IsEnabled = false;

            var (apps, result) = await _wingetService.SearchAsync(query);
            foreach (var app in apps) UpdateApps.Add(app);

            UpdateStatus.Content = result.Success
                ? $"Found {UpdateApps.Count} results. Select a package ID to install."
                : $"Winget search failed: {DescribeCommandFailure(result)}";
            WingetSearchBtn.IsEnabled = true;
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

        private void LoadDefragDrives()
        {
            DefragDriveCombo.Items.Clear();
            foreach (var drive in _defragService.LoadFixedDrives())
            {
                DefragDriveCombo.Items.Add(drive);
            }

            if (DefragDriveCombo.Items.Count > 0) DefragDriveCombo.SelectedIndex = 0;
        }

        private void DefragLogMsg(string msg)
        {
            DefragLogBox.AppendText($"{msg}{Environment.NewLine}");
            DefragLogBox.ScrollToEnd();
        }

        private async void AnalyzeDriveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DefragDriveCombo.SelectedItem == null || !EnsureAdministrator("Drive analysis uses defrag.exe and requires administrator rights."))
            {
                return;
            }

            await RunDefragUiAsync(optimize: false);
        }

        private async void OptimizeDriveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DefragDriveCombo.SelectedItem == null || !EnsureAdministrator("Drive optimization requires administrator rights."))
            {
                return;
            }

            await RunDefragUiAsync(optimize: true);
        }

        private void CancelDefragBtn_Click(object sender, RoutedEventArgs e)
        {
            _defragCts?.Cancel();
        }

        private async Task RunDefragUiAsync(bool optimize)
        {
            string drive = DefragDriveCombo.SelectedItem?.ToString() ?? "";
            DefragLogBox.Clear();
            AnalyzeDriveBtn.IsEnabled = false;
            OptimizeDriveBtn.IsEnabled = false;
            CancelDefragBtn.IsEnabled = true;
            _defragCts = new CancellationTokenSource();
            DefragStatus.Content = optimize ? $"Optimizing {drive}..." : $"Analyzing {drive}...";

            try
            {
                CommandResult result = optimize
                    ? await _defragService.OptimizeAsync(drive, chkSsdTrim.IsChecked == true, chkBootDefrag.IsChecked == true, _defragCts.Token)
                    : await _defragService.AnalyzeAsync(drive, _defragCts.Token);

                if (!string.IsNullOrWhiteSpace(result.StandardOutput)) DefragLogMsg(result.StandardOutput.TrimEnd());
                if (!string.IsNullOrWhiteSpace(result.StandardError)) DefragLogMsg("ERROR: " + result.StandardError.TrimEnd());
                DefragStatus.Content = result.Success ? "Defrag command complete." : "Defrag command failed.";
                if (!result.Success) DefragLogMsg(DescribeCommandFailure(result));
            }
            finally
            {
                _defragCts.Dispose();
                _defragCts = null;
                AnalyzeDriveBtn.IsEnabled = true;
                OptimizeDriveBtn.IsEnabled = true;
                CancelDefragBtn.IsEnabled = false;
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
