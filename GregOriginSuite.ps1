<#
    .SYNOPSIS
    GregOrigin Suite
    .DESCRIPTION
    Fixes text encoding issues and adds interactive hover states.
#>

# --- Load WPF Assemblies ---
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# --- XAML UI Definition ---
[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="GregOrigin Cleaner Suite" Height="720" Width="950" 
        WindowStartupLocation="CenterScreen" FontFamily="Segoe UI Light"
        Background="#F3F9FF">
    
    <Window.Resources>
        <!-- MASTER BUTTON STYLE -->
        <Style TargetType="Button">
            <Setter Property="Background" Value="#0078D7"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="Padding" Value="15,6"/>
            <Setter Property="Margin" Value="5"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="FontSize" Value="14"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="4">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        
                        <!-- HOVER TRIGGERS -->
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <!-- Lightens the button color on hover -->
                                <Setter TargetName="border" Property="Opacity" Value="0.75"/> 
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="border" Property="Opacity" Value="1.0"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        
        <!-- ListView Style -->
        <Style TargetType="ListView">
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Background" Value="White"/>
            <Setter Property="Foreground" Value="#333"/>
            <Setter Property="FontSize" Value="13"/>
        </Style>

        <Style TargetType="TabItem">
            <Setter Property="FontSize" Value="16"/>
            <Setter Property="Padding" Value="15,10"/>
        </Style>
    </Window.Resources>
    
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/> <!-- Header -->
            <RowDefinition Height="*"/>    <!-- Content -->
        </Grid.RowDefinitions>

        <!-- HEADER: GregOrigin Branding -->
        <Border Grid.Row="0" Background="#004E8C" Padding="20">
            <DockPanel LastChildFill="False">
                <StackPanel Orientation="Horizontal" DockPanel.Dock="Left">
                    <TextBlock Text="Greg" Foreground="White" FontSize="24" FontWeight="Bold"/>
                    <TextBlock Text="Origin" Foreground="#80D4FF" FontSize="24" FontWeight="Light"/>
                    <TextBlock Text=" CLEANER SUITE" Foreground="#A9D9F5" FontSize="14" VerticalAlignment="Bottom" Margin="5,0,0,4"/>
                </StackPanel>
                
                <!-- Website Link (Fixed Encoding) -->
                <Button Name="SiteBtn" Content="Visit gregorigin.com &#x2197;" DockPanel.Dock="Right" 
                        Background="Transparent" Foreground="White" FontSize="14" FontWeight="Normal"/>
            </DockPanel>
        </Border>

        <!-- MAIN TABS -->
        <TabControl Grid.Row="1" BorderThickness="0" Background="Transparent" Margin="10">
            
            <!-- TAB 1: UNINSTALLER -->
            <TabItem Header="Uninstall">
                <Grid Margin="10">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    
                    <TextBox Name="SearchBox" Grid.Row="0" Height="35" VerticalContentAlignment="Center" 
                             Padding="5" Margin="0,0,0,15" BorderBrush="#ADD8E6" BorderThickness="1" 
                             Text="Search installed apps..."/>
                    
                    <ListView Name="AppList" Grid.Row="1" SelectionMode="Extended">
                        <ListView.View>
                            <GridView>
                                <GridViewColumn Header="Name" Width="400" DisplayMemberBinding="{Binding DisplayName}"/>
                                <GridViewColumn Header="Publisher" Width="250" DisplayMemberBinding="{Binding Publisher}"/>
                                <GridViewColumn Header="Ver" Width="100" DisplayMemberBinding="{Binding DisplayVersion}"/>
                            </GridView>
                        </ListView.View>
                    </ListView>
                    
                    <DockPanel Grid.Row="2" Margin="0,15,0,0">
                        <Label Name="StatusLabel" Content="Ready" Foreground="#004E8C" VerticalAlignment="Center"/>
                        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                            <Button Name="RefreshBtn" Content="Refresh" Background="#666"/>
                            <Button Name="UninstallBtn" Content="Uninstall Selected" Background="#C42B1C"/>
                        </StackPanel>
                    </DockPanel>
                </Grid>
            </TabItem>

            <!-- TAB 2: CLEANER -->
            <TabItem Header="Clean">
                <Grid Margin="30">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>

                    <TextBlock Grid.Row="0" Text="Safe System Cleaning" FontSize="22" Foreground="#004E8C" Margin="0,0,0,20"/>
                    
                    <Border Grid.Row="1" Background="White" Padding="15" CornerRadius="5">
                        <WrapPanel Orientation="Horizontal">
                            <CheckBox Name="chkWinTemp" Content="Windows Temp" IsChecked="True" Margin="0,0,20,10" FontSize="14"/>
                            <CheckBox Name="chkUserTemp" Content="User Temp" IsChecked="True" Margin="0,0,20,10" FontSize="14"/>
                            <CheckBox Name="chkEdge" Content="Edge Cache" IsChecked="True" Margin="0,0,20,10" FontSize="14"/>
                            <CheckBox Name="chkChrome" Content="Chrome Cache" IsChecked="True" Margin="0,0,20,10" FontSize="14"/>
                            <CheckBox Name="chkRecycle" Content="Empty Recycle Bin" IsChecked="False" Margin="0,0,20,10" FontSize="14"/>
                        </WrapPanel>
                    </Border>

                    <TextBox Name="LogBox" Grid.Row="2" Margin="0,20,0,20" IsReadOnly="True" 
                             VerticalScrollBarVisibility="Auto" Background="White" BorderThickness="0" 
                             Padding="10" FontFamily="Consolas" Foreground="#555"/>

                    <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Right">
                        <Button Name="ScanBtn" Content="Analyze" Background="#0078D7"/>
                        <Button Name="CleanBtn" Content="Run Cleaner" Background="#28A745"/>
                    </StackPanel>
                </Grid>
            </TabItem>

            <!-- TAB 3: UPDATER -->
            <TabItem Header="Update">
                <Grid Margin="10">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    
                    <TextBlock Grid.Row="0" Text="Powered by Microsoft Winget" FontSize="14" Foreground="#0078D7" Margin="0,0,0,10" HorizontalAlignment="Right"/>
                    
                    <ListView Name="UpdateList" Grid.Row="1">
                        <ListView.View>
                            <GridView>
                                <GridViewColumn Header="Application" Width="300" DisplayMemberBinding="{Binding Name}"/>
                                <GridViewColumn Header="Current" Width="150" DisplayMemberBinding="{Binding Current}"/>
                                <GridViewColumn Header="Newest Available" Width="150" DisplayMemberBinding="{Binding Available}"/>
                                <GridViewColumn Header="Source" Width="100" DisplayMemberBinding="{Binding Source}"/>
                            </GridView>
                        </ListView.View>
                    </ListView>

                    <DockPanel Grid.Row="2" Margin="0,15,0,0">
                        <Label Name="UpdateStatus" Content="Check for updates to begin." Foreground="#004E8C" VerticalAlignment="Center"/>
                        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                            <Button Name="CheckUpdateBtn" Content="Check Updates"/>
                            <Button Name="UpdateAllBtn" Content="Update All" Background="#004E8C"/>
                        </StackPanel>
                    </DockPanel>
                </Grid>
            </TabItem>
        </TabControl>
    </Grid>
</Window>
"@

$reader = (New-Object System.Xml.XmlNodeReader $xaml)
$window = [Windows.Markup.XamlReader]::Load($reader)

# --- MAP ELEMENTS ---
$SiteBtn = $window.FindName("SiteBtn")
$AppList = $window.FindName("AppList"); $SearchBox = $window.FindName("SearchBox"); $UninstallBtn = $window.FindName("UninstallBtn"); $RefreshBtn = $window.FindName("RefreshBtn"); $StatusLabel = $window.FindName("StatusLabel")
$LogBox = $window.FindName("LogBox"); $ScanBtn = $window.FindName("ScanBtn"); $CleanBtn = $window.FindName("CleanBtn")
$chkWinTemp = $window.FindName("chkWinTemp"); $chkUserTemp = $window.FindName("chkUserTemp"); $chkEdge = $window.FindName("chkEdge"); $chkChrome = $window.FindName("chkChrome"); $chkRecycle = $window.FindName("chkRecycle")
$UpdateList = $window.FindName("UpdateList"); $CheckUpdateBtn = $window.FindName("CheckUpdateBtn"); $UpdateAllBtn = $window.FindName("UpdateAllBtn"); $UpdateStatus = $window.FindName("UpdateStatus")

# --- GLOBAL EVENTS ---
$SiteBtn.Add_Click({ Start-Process "https://gregorigin.com" })

# --- UNINSTALLER LOGIC ---
function Get-InstalledApps {
    $StatusLabel.Content = "Scanning Registry..."
    [System.Windows.Forms.Application]::DoEvents()
    $AppList.Items.Clear()
    $paths = @("HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*","HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*","HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*")
    $apps = Get-ItemProperty $paths -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -and $_.UninstallString -and $_.SystemComponent -ne 1 } | Sort-Object DisplayName -Unique
    foreach ($app in $apps) { $AppList.Items.Add($app) }
    $StatusLabel.Content = "Found $($AppList.Items.Count) applications."
}
$RefreshBtn.Add_Click({ Get-InstalledApps })
$UninstallBtn.Add_Click({
    $selected = $AppList.SelectedItems
    if ($selected.Count -gt 0 -and [System.Windows.MessageBox]::Show("Uninstall $($selected.Count) apps?", "GregOrigin Suite", "YesNo") -eq "Yes") {
        foreach ($app in $selected) {
            $StatusLabel.Content = "Removing: $($app.DisplayName)"
            [System.Windows.Forms.Application]::DoEvents()
            try {
                $u = $app.UninstallString
                if ($u -match "msiexec") { $args = $u -replace "msiexec.exe","" -replace "msiexec",""; Start-Process "msiexec.exe" -ArgumentList "$args /qb" -Wait -NoNewWindow }
                else { $proc = New-Object System.Diagnostics.ProcessStartInfo; $proc.FileName = "cmd.exe"; $proc.Arguments = "/c `"$u`""; $proc.WindowStyle = "Hidden"; $proc.Verb = "runas"; [System.Diagnostics.Process]::Start($proc).WaitForExit() }
            } catch {}
        }
        Get-InstalledApps
    }
})
$SearchBox.Add_TextChanged({ $AppList.Items.Filter = [Predicate[Object]]{ param($item) return ($item.DisplayName -match $SearchBox.Text) } })

# --- CLEANER LOGIC ---
function Log-Msg ($msg) { $LogBox.AppendText("$msg`n"); $LogBox.ScrollToEnd() }
function Get-Targets {
    $t = @()
    if ($chkWinTemp.IsChecked) { $t += @{Name="Windows Temp"; Path="C:\Windows\Temp\*"}}
    if ($chkUserTemp.IsChecked) { $t += @{Name="User Temp"; Path="$env:TEMP\*"}}
    if ($chkEdge.IsChecked) { $t += @{Name="Edge Cache"; Path="$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Cache\Cache_Data\*"}}
    if ($chkChrome.IsChecked) { $t += @{Name="Chrome Cache"; Path="$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Cache\Cache_Data\*"}}
    return $t
}
$ScanBtn.Add_Click({
    $LogBox.Clear(); $CleanBtn.IsEnabled = $false
    $list = Get-Targets
    foreach ($t in $list) { 
        if (Test-Path $t.Path) { 
            $s = (Get-ChildItem $t.Path -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
            Log-Msg "Detected $t.Name : $([Math]::Round($s/1MB,2)) MB" 
        } 
    }
    $CleanBtn.IsEnabled = $true; Log-Msg "Analysis Complete."
})
$CleanBtn.Add_Click({
    if ([System.Windows.MessageBox]::Show("Clean these files?", "GregOrigin Suite", "YesNo") -eq "Yes") {
        Log-Msg "Cleaning starts..."; Get-Targets | ForEach-Object { Get-ChildItem $_.Path -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue }; 
        if ($chkRecycle.IsChecked) { Clear-RecycleBin -Force -ErrorAction SilentlyContinue; Log-Msg "Recycle Bin Emptied" }
        Log-Msg "System Cleaned."
    }
})

# --- WINGET UPDATER LOGIC ---
$CheckUpdateBtn.Add_Click({
    $UpdateStatus.Content = "Contacting Winget servers..."
    $UpdateList.Items.Clear()
    [System.Windows.Forms.Application]::DoEvents()
    
    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo.FileName = "winget.exe"
    $proc.StartInfo.Arguments = "upgrade"
    $proc.StartInfo.RedirectStandardOutput = $true
    $proc.StartInfo.UseShellExecute = $false
    $proc.StartInfo.CreateNoWindow = $true
    $proc.StartInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $proc.Start() | Out-Null
    $output = $proc.StandardOutput.ReadToEnd()
    $proc.WaitForExit()

    $lines = $output -split "`n"
    $foundStart = $false
    foreach ($line in $lines) {
        if ($line -match "^Name\s+Id\s+Version") { $foundStart = $true; continue }
        if (-not $foundStart -or $line -match "^-") { continue }
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        
        $parts = $line -split "\s{2,}"
        if ($parts.Count -ge 3) {
            $item = New-Object PSObject -Property @{ Name = $parts[0]; Current = $parts[2]; Available = $parts[3]; Source = "Winget" }
            $UpdateList.Items.Add($item)
        }
    }
    $UpdateStatus.Content = "Found $($UpdateList.Items.Count) updates."
})

$UpdateAllBtn.Add_Click({
    if ($UpdateList.Items.Count -eq 0) { return }
    if ([System.Windows.MessageBox]::Show("Launch Winget to update all?", "GregOrigin Suite", "YesNo") -eq "Yes") {
        Start-Process "winget.exe" -ArgumentList "upgrade --all --include-unknown"
    }
})

# --- INIT ---
Get-InstalledApps
$window.ShowDialog() | Out-Null