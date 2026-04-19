using GregOriginSuiteApp.Services;

namespace GregOriginSuiteApp.Tests;

public sealed class CleanupServiceTests
{
    [Fact]
    public async Task BuildPlanForTargets_CapturesExactFilesAndBytesWithoutDeleting()
    {
        string root = Path.Combine(Path.GetTempPath(), "GregOriginSuiteTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string filePath = Path.Combine(root, "cache.bin");
        await File.WriteAllBytesAsync(filePath, new byte[] { 1, 2, 3, 4 });

        try
        {
            var service = new CleanupService(new FakeProcessRunner());
            CleanupPlan plan = await service.BuildPlanForTargetsAsync(new[]
            {
                new CleanupTarget { Name = "Test Target", Path = root }
            });

            Assert.Single(plan.Files);
            Assert.Equal(filePath, plan.Files[0].Path);
            Assert.Equal(4, plan.Files[0].Bytes);
            Assert.True(File.Exists(filePath));
            Assert.True(File.Exists(plan.AuditPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CommandResult
            {
                FileName = spec.FileName,
                Arguments = spec.Arguments,
                Started = true,
                ExitCode = 0
            });
        }
    }
}
