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

            <!-- TAB 4: STARTUP APPS -->
            <TabItem Header="Startup">
                <Grid Margin="10">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    
                    <TextBlock Grid.Row="0" Text="Manage Startup Applications" FontSize="22" Foreground="#004E8C" Margin="0,0,0,15"/>
                    
                    <ListView Name="StartupList" Grid.Row="1" SelectionMode="Extended">
                        <ListView.View>
                            <GridView>
                                <GridViewColumn Header="Name" Width="200" DisplayMemberBinding="{Binding Name}"/>
                                <GridViewColumn Header="Command" Width="300" DisplayMemberBinding="{Binding Command}"/>
                                <GridViewColumn Header="Location" Width="200" DisplayMemberBinding="{Binding Location}"/>
                                <GridViewColumn Header="State" Width="100" DisplayMemberBinding="{Binding State}"/>
                            </GridView>
                        </ListView.View>
                    </ListView>

                    <DockPanel Grid.Row="2" Margin="0,15,0,0">
                        <Label Name="StartupStatus" Content="Ready" Foreground="#004E8C" VerticalAlignment="Center"/>
                        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                            <Button Name="RefreshStartupBtn" Content="Refresh" Background="#666"/>
                            <Button Name="ToggleStartupBtn" Content="Toggle Enable/Disable" Background="#E0A800"/>
                            <Button Name="DeleteStartupBtn" Content="Delete Selected" Background="#C42B1C"/>
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
$LogBox = $window.FindName("LogBox"); $ScanBtn = $window.FindName("ScanBtn"); $CleanBtn = $window.FindName("CleanBtn")
$chkWinTemp = $window.FindName("chkWinTemp"); $chkUserTemp = $window.FindName("chkUserTemp"); $chkEdge = $window.FindName("chkEdge"); $chkChrome = $window.FindName("chkChrome"); $chkRecycle = $window.FindName("chkRecycle")
$StartupList = $window.FindName("StartupList"); $RefreshStartupBtn = $window.FindName("RefreshStartupBtn"); $ToggleStartupBtn = $window.FindName("ToggleStartupBtn"); $DeleteStartupBtn = $window.FindName("DeleteStartupBtn"); $StartupStatus = $window.FindName("StartupStatus")

# --- GLOBAL EVENTS ---
$SiteBtn.Add_Click({ Start-Process "https://gregorigin.com" })

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

# --- STARTUP LOGIC ---
function Get-StartupApps {
    $StartupStatus.Content = "Scanning Startup items..."
    [System.Windows.Forms.Application]::DoEvents()
    $StartupList.Items.Clear()
    
    $locations = @(
        @{ Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"; Name="HKCU Run" },
        @{ Path="HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; Name="HKLM Run" },
        @{ Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Run_Disabled"; Name="HKCU Run" },
        @{ Path="HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run_Disabled"; Name="HKLM Run" }
    )
    
    foreach ($loc in $locations) {
        if (Test-Path $loc.Path) {
            $state = if ($loc.Path -match "_Disabled") { "Disabled" } else { "Enabled" }
            (Get-Item $loc.Path).Property | ForEach-Object {
                $val = (Get-ItemProperty $loc.Path).$_
                $item = New-Object PSObject -Property @{ Name = $_; Command = $val; Location = $loc.Name; State = $state; Key = $loc.Path }
                $StartupList.Items.Add($item) | Out-Null
            }
        }
    }

    $folders = @(
        @{ Path="$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup"; Name="User Startup Folder" },
        @{ Path="$env:ALLUSERSPROFILE\Microsoft\Windows\Start Menu\Programs\Startup"; Name="All Users Startup Folder" }
    )

    foreach ($folder in $folders) {
        if (Test-Path $folder.Path) {
            Get-ChildItem $folder.Path -Filter "*.lnk" | ForEach-Object {
                $item = New-Object PSObject -Property @{ Name = $_.Name; Command = $_.FullName; Location = $folder.Name; State = "Enabled"; Key = $_.FullName }
                $StartupList.Items.Add($item) | Out-Null
            }
            Get-ChildItem $folder.Path -Filter "*.lnk_disabled" | ForEach-Object {
                $item = New-Object PSObject -Property @{ Name = $_.Name; Command = $_.FullName; Location = $folder.Name; State = "Disabled"; Key = $_.FullName }
                $StartupList.Items.Add($item) | Out-Null
            }
        }
    }

    $StartupStatus.Content = "Found $($StartupList.Items.Count) startup items."
}

$RefreshStartupBtn.Add_Click({ Get-StartupApps })

$ToggleStartupBtn.Add_Click({
    $selected = $StartupList.SelectedItems
    if ($selected.Count -eq 0) { return }
    
    foreach ($app in $selected) {
        if ($app.Location -match "Run") {
            if ($app.State -eq "Enabled") {
                $targetKey = $app.Key + "_Disabled"
                if (-not (Test-Path $targetKey)) { New-Item -Path $targetKey -Force | Out-Null }
                Set-ItemProperty -Path $targetKey -Name $app.Name -Value $app.Command
                Remove-ItemProperty -Path $app.Key -Name $app.Name
            } else {
                $targetKey = $app.Key -replace "_Disabled",""
                if (-not (Test-Path $targetKey)) { New-Item -Path $targetKey -Force | Out-Null }
                Set-ItemProperty -Path $targetKey -Name $app.Name -Value $app.Command
                Remove-ItemProperty -Path $app.Key -Name $app.Name
            }
        } else {
            if ($app.State -eq "Enabled") {
                Rename-Item -Path $app.Key -NewName ($app.Name + "_disabled")
            } else {
                Rename-Item -Path $app.Key -NewName ($app.Name -replace "_disabled$","")
            }
        }
    }
    Get-StartupApps
})

$DeleteStartupBtn.Add_Click({
    $selected = $StartupList.SelectedItems
    if ($selected.Count -eq 0) { return }
    
    if ([System.Windows.MessageBox]::Show("Delete $($selected.Count) startup items? This cannot be undone.", "GregOrigin Suite", "YesNo") -eq "Yes") {
        foreach ($app in $selected) {
            if ($app.Location -match "Run") {
                Remove-ItemProperty -Path $app.Key -Name $app.Name -ErrorAction SilentlyContinue
            } else {
                Remove-Item -Path $app.Key -Force -ErrorAction SilentlyContinue
            }
        }
        Get-StartupApps
    }
})

# --- INIT ---
Get-StartupApps
$window.ShowDialog() | Out-Null