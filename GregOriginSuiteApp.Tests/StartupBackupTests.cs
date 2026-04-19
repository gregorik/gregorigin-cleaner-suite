using GregOriginSuiteApp.Models;
using GregOriginSuiteApp.Services;
using Microsoft.Win32;

namespace GregOriginSuiteApp.Tests;

public sealed class StartupBackupTests
{
    [Fact]
    public void FromStartupApp_PreservesRegistryValueKindForRecovery()
    {
        var app = new StartupApp
        {
            Name = "Demo",
            Command = "%USERPROFILE%\\demo.exe",
            RegistryHive = "HKCU",
            RegistryPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run",
            RegistryValueKind = RegistryValueKind.ExpandString
        };

        StartupBackupEntry backup = StartupBackupEntry.FromStartupApp(app, "Disabled");

        Assert.Equal(RegistryValueKind.ExpandString, backup.RegistryValueKind);
        Assert.Equal("HKCU", backup.RegistryHive);
        Assert.Equal(app.RegistryPath, backup.RegistryPath);
    }
}
