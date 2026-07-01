namespace DRN.Test.Unit.Tests.Framework.Testing.Contexts;

public class DrnTestContextTempPathTests
{
    [Theory]
    [DataInlineUnit]
    public void DrnTestContextUnit_GetTempPath_Should_Return_MethodContext_TempPath(DrnTestContextUnit context)
    {
        var tempPath = context.GetTempPath();
        var methodTempPath = context.MethodContext.GetTempPath();

        tempPath.Should().Be(methodTempPath);
        Directory.Exists(tempPath).Should().BeTrue();
        tempPath.Should().StartWith(AppConstants.TempPath);
        tempPath.Should().Contain(nameof(DrnTestContextUnit_GetTempPath_Should_Return_MethodContext_TempPath));
    }

    [Theory]
    [DataInlineUnit]
    public async Task MethodContext_GetTempPath_Should_Return_Same_Path_For_Concurrent_First_Access(DrnTestContextUnit context)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pathTasks = Enumerable.Range(0, 64)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task;
                return context.MethodContext.GetTempPath();
            }))
            .ToArray();

        start.SetResult();
        var paths = await Task.WhenAll(pathTasks);

        paths.Should().OnlyContain(path => path == paths[0]);
        Directory.Exists(paths[0]).Should().BeTrue();
    }

    [Fact]
    public void DrnTestContextUnit_Dispose_Should_Aggregate_Failures_And_Be_Idempotent()
    {
        var context = CreateContext(nameof(DrnTestContextUnit_Dispose_Should_Aggregate_Failures_And_Be_Idempotent));
        context.ServiceCollection.AddSingleton<ThrowingDisposable>();
        var throwingDisposable = context.GetRequiredService<ThrowingDisposable>();
        var tempPath = context.GetTempPath();
        Directory.Exists(tempPath).Should().BeTrue();

        var firstDispose = () => context.Dispose();

        var exception = firstDispose.Should().Throw<AggregateException>().Which;
        exception.InnerExceptions.Should().ContainSingle()
            .Which.Should().BeSameAs(ThrowingDisposable.Exception);
        throwingDisposable.DisposeCount.Should().Be(1);
        Directory.Exists(tempPath).Should().BeFalse();

        var secondDispose = () => context.Dispose();

        secondDispose.Should().NotThrow();
        throwingDisposable.DisposeCount.Should().Be(1);
        context.ServiceCollection.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit]
    public void DrnTestContextUnit_GetTempPath_Should_Throw_ObjectDisposedException_After_Disposal(DrnTestContextUnit context)
    {
        var tempPath = context.GetTempPath();
        Directory.Exists(tempPath).Should().BeTrue();

        context.Dispose();

        var act = () => context.GetTempPath();
        act.Should().Throw<ObjectDisposedException>();
    }

    private static DrnTestContextUnit CreateContext(string testMethodName)
    {
        var testMethod = typeof(DrnTestContextTempPathTests).GetMethod(testMethodName)!;
        return new DrnTestContextUnit(testMethod);
    }

    private sealed class ThrowingDisposable : IDisposable
    {
        public static readonly InvalidOperationException Exception = new("Dispose failure for testing.");
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            throw Exception;
        }
    }
}
