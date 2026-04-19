using GregOriginSuiteApp.Services;

namespace GregOriginSuiteApp.Tests;

public sealed class WingetParserTests
{
    [Fact]
    public void ParseUpgradeOutput_UsesPackageIdAndAvailableVersion()
    {
        const string output = """
        Name                           Id                         Version      Available    Source
        ------------------------------------------------------------------------------------------
        Microsoft PowerToys            Microsoft.PowerToys        0.80.0       0.81.0       winget
        Visual Studio Code             Microsoft.VisualStudioCode 1.90.0       1.91.0       winget
        """;

        var apps = WingetParser.ParseUpgradeOutput(output);

        Assert.Equal(2, apps.Count);
        Assert.Equal("Microsoft PowerToys", apps[0].Name);
        Assert.Equal("Microsoft.PowerToys", apps[0].Id);
        Assert.Equal("0.80.0", apps[0].Current);
        Assert.Equal("0.81.0", apps[0].Available);
    }

    [Fact]
    public void BuildUpgradeCommand_TargetsExactPackageId()
    {
        var command = WingetParser.BuildUpgradeCommand(new() { Id = "Microsoft.PowerToys" });

        Assert.Equal("winget.exe", command.FileName);
        Assert.Contains("upgrade --id Microsoft.PowerToys --exact", command.Arguments);
    }
}
