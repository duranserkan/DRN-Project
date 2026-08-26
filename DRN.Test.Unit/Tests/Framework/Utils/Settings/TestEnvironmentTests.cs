using DRN.Framework.Utils.Settings;

namespace DRN.Test.Unit.Tests.Framework.Utils.Settings;

public class TestEnvironmentTests
{
    [Fact]
    public void SetTestContextEnabledScope_Should_Override_Value_And_Restore_On_Dispose()
    {
        var initialState = TestEnvironment.DrnTestContextEnabled;

        using (TestEnvironment.SetTestContextEnabledScope(!initialState))
        {
            TestEnvironment.DrnTestContextEnabled.Should().Be(!initialState);
        }

        TestEnvironment.DrnTestContextEnabled.Should().Be(initialState);
    }

    [Fact]
    public void SetTestContextEnabledScope_Should_Support_Nested_Scopes()
    {
        var initialState = TestEnvironment.DrnTestContextEnabled;

        using (TestEnvironment.SetTestContextEnabledScope(!initialState))
        {
            TestEnvironment.DrnTestContextEnabled.Should().Be(!initialState);

            using (TestEnvironment.SetTestContextEnabledScope(initialState))
            {
                TestEnvironment.DrnTestContextEnabled.Should().Be(initialState);
            }

            TestEnvironment.DrnTestContextEnabled.Should().Be(!initialState);
        }

        TestEnvironment.DrnTestContextEnabled.Should().Be(initialState);
    }

    [Fact]
    public async Task SetTestContextEnabledScope_Should_Isolate_Across_Async_Contexts()
    {
        var initialState = TestEnvironment.DrnTestContextEnabled;

        using (TestEnvironment.SetTestContextEnabledScope(false))
        {
            TestEnvironment.DrnTestContextEnabled.Should().BeFalse();

            var taskWithTrue = Task.Run(() =>
            {
                using var _ = TestEnvironment.SetTestContextEnabledScope(true);
                return TestEnvironment.DrnTestContextEnabled;
            });

            var result = await taskWithTrue;
            result.Should().BeTrue();
            TestEnvironment.DrnTestContextEnabled.Should().BeFalse();
        }

        TestEnvironment.DrnTestContextEnabled.Should().Be(initialState);
    }
}
