using GregOriginSuiteApp.Services;

namespace GregOriginSuiteApp.Tests;

public sealed class UninstallCommandBuilderTests
{
    [Fact]
    public void Build_ConvertsMsiInstallProductCodeToUninstall()
    {
        CommandSpec spec = UninstallCommandBuilder.Build("MsiExec.exe /I{11111111-2222-3333-4444-555555555555}");

        Assert.Equal("msiexec.exe", spec.FileName);
        Assert.Contains("/X{11111111-2222-3333-4444-555555555555}", spec.Arguments);
        Assert.Contains("/qb", spec.Arguments);
    }

    [Fact]
    public void Build_KeepsNonMsiExecutableOutOfCmdWrapper()
    {
        CommandSpec spec = UninstallCommandBuilder.Build("\"C:\\Program Files\\Vendor\\uninstall.exe\" /remove /quiet");

        Assert.Equal("C:\\Program Files\\Vendor\\uninstall.exe", spec.FileName);
        Assert.Equal("/remove /quiet", spec.Arguments);
        Assert.True(spec.UseShellExecute);
    }
}
