namespace DRN.Test.Integration.Tests.Framework.Testing.Contexts;

public class DrnTestContextTempPathTests
{
    [Theory]
    [DataInline]
    public void DrnTestContext_GetTempPath_Should_Return_MethodContext_TempPath(DrnTestContext context)
    {
        var tempPath = context.GetTempPath();
        var methodTempPath = context.MethodContext.GetTempPath();

        tempPath.Should().Be(methodTempPath);
        Directory.Exists(tempPath).Should().BeTrue();
        tempPath.Should().StartWith(AppConstants.TempPath);
        tempPath.Should().Contain(nameof(DrnTestContext_GetTempPath_Should_Return_MethodContext_TempPath));
    }

    [Fact]
    public void DrnTestContext_Dispose_Should_Aggregate_Failures_And_Be_Idempotent()
    {
        var context = CreateContext(nameof(DrnTestContext_Dispose_Should_Aggregate_Failures_And_Be_Idempotent));
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

    [Fact]
    public void DrnTestContext_Dispose_Should_Not_Create_FlurlHttpTest_When_Unused()
    {
        var context = CreateContext(nameof(DrnTestContext_Dispose_Should_Not_Create_FlurlHttpTest_When_Unused));
        var flurlHttpTest = GetFlurlHttpTestLazy(context);

        context.Dispose();

        IsLazyValueCreated(flurlHttpTest).Should().BeFalse();
    }

    private static DrnTestContext CreateContext(string testMethodName)
    {
        var testMethod = typeof(DrnTestContextTempPathTests).GetMethod(testMethodName)!;
        return new DrnTestContext(testMethod);
    }

    private static object GetFlurlHttpTestLazy(DrnTestContext context)
    {
        var field = typeof(DrnTestContext).GetField("_flurlHttpTest", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return field.GetValue(context)!;
    }

    private static bool IsLazyValueCreated(object lazy) =>
        (bool)lazy.GetType().GetProperty(nameof(Lazy<object>.IsValueCreated))!.GetValue(lazy)!;

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
