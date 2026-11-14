using DRN.Framework.Utils.Logging;
using DRN.Test.Utils.Hosting;
using Microsoft.AspNetCore.Builder;

namespace DRN.Test.Integration.Tests.Framework.Hosting;

public class DrnProgramActionsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly string[] Arguments = ["--Environment=Development"];

    [Theory]
    [DataInline(false, false)]
    [DataInline(false, true)]
    [DataInline(true, false)]
    [DataInline(true, true)]
    public async Task Startup_Should_Await_Each_Hook_In_Order(bool temporary, bool skipValidation)
    {
        using var settings = SettingsProvider.Development(new
        {
            DrnDevelopmentSettings = new { TemporaryApplication = temporary, SkipValidation = skipValidation }
        });
        var log = Substitute.For<IScopedLog>();
        var probe = new ProgramActionsProbe();
        var create = ProgramActionsTestProgram.CreateApplicationAsync(Arguments, settings, log, probe.ConfigureBuilder);
        string[][] pendingEvents =
        [
            ["builder:configured", "hook:0:start"],
            ["builder:configured", "hook:0:start", "hook:0:end", "pipeline", "hook:1:start"],
            ["builder:configured", "hook:0:start", "hook:0:end", "pipeline", "hook:1:start", "hook:1:end",
                "endpoints", "services:start", "services:end", "hook:2:start"]
        ];

        try
        {
            for (var hook = 0; hook < probe.Gates.Length; hook++)
            {
                await probe.Gates[hook].Entered.Task.WaitAsync(Timeout);
                create.IsCompleted.Should().BeFalse();
                probe.Events.Should().Equal(pendingEvents[hook]);
                if (hook == 0) probe.Application.Should().BeNull();
                probe.Gates[hook].Release.SetResult();
            }

            var application = await create.WaitAsync(Timeout);
            application.Should().BeSameAs(probe.Application);
            probe.Events.Should().Equal(pendingEvents[2].Append("hook:2:end"));
            probe.Builder.Should().NotBeNull();
            probe.Arguments.Should().HaveCount(3);
            probe.Arguments.Should().OnlyContain(a => ReferenceEquals(a.Program, probe.Arguments[0].Program)
                && ReferenceEquals(a.Settings, settings) && ReferenceEquals(a.Log, log));
            probe.ActionInstances.Should().HaveCount(3).And.OnlyContain(a => ReferenceEquals(a, probe.ActionInstances[0]));
            probe.ValidationResolutions.Should().Be(skipValidation ? 0 : 1);
        }
        finally
        {
            await CleanupAsync(probe, create);
        }
    }

    [Theory]
    [DataInline(0)]
    [DataInline(1)]
    [DataInline(2)]
    public async Task Hook_Failure_Should_Propagate_And_Prevent_Later_Stages(int hook)
    {
        using var settings = SettingsProvider.Development();
        var probe = new ProgramActionsProbe { FailingHook = hook };
        var create = ProgramActionsTestProgram.CreateApplicationAsync(Arguments, settings, Substitute.For<IScopedLog>(), probe.ConfigureBuilder);

        try
        {
            for (var current = 0; current <= hook; current++)
            {
                await probe.Gates[current].Entered.Task.WaitAsync(Timeout);
                create.IsCompleted.Should().BeFalse();
                probe.Gates[current].Release.SetResult();
            }

            Func<Task> startup = () => create.WaitAsync(Timeout);
            var thrown = await startup.Should().ThrowExactlyAsync<InvalidOperationException>();
            thrown.Which.Should().BeSameAs(probe.Failure);
            probe.Arguments.Should().HaveCount(hook + 1);
            probe.Events.Last().Should().Be($"hook:{hook}:start");
            probe.ValidationResolutions.Should().Be(hook == 2 ? 1 : 0);
            if (hook == 0) probe.Application.Should().BeNull();
        }
        finally
        {
            await CleanupAsync(probe, create);
        }
    }

    [Theory]
    [DataInline(false)]
    [DataInline(true)]
    public async Task Temporary_Startup_Should_Honor_Service_Validation_Skip_Before_Validated_Hook(bool skipValidation)
    {
        using var settings = SettingsProvider.Development(new
        {
            DrnDevelopmentSettings = new { TemporaryApplication = true, SkipValidation = skipValidation }
        });
        var probe = new ProgramActionsProbe { FailValidation = true };
        probe.ReleaseAll();
        var create = ProgramActionsTestProgram.CreateApplicationAsync(Arguments, settings, Substitute.For<IScopedLog>(), probe.ConfigureBuilder);

        try
        {
            if (skipValidation)
            {
                await create.WaitAsync(Timeout);
                probe.Events.Last().Should().Be("hook:2:end");
                probe.ValidationResolutions.Should().Be(0);
            }
            else
            {
                Func<Task> startup = () => create.WaitAsync(Timeout);
                var thrown = await startup.Should().ThrowExactlyAsync<InvalidOperationException>();
                thrown.Which.Should().BeSameAs(probe.Failure);
                probe.Events.Last().Should().Be("services:start");
                probe.Gates[2].Entered.Task.IsCompleted.Should().BeFalse();
                probe.ValidationResolutions.Should().Be(1);
            }
        }
        finally
        {
            await CleanupAsync(probe, create);
        }
    }

    [Theory]
    [DataInline(false, false, false)]
    [DataInline(false, false, true)]
    [DataInline(false, true, true)]
    [DataInline(true, false, false)]
    [DataInline(true, false, true)]
    [DataInline(true, true, true)]
    public async Task Startup_Should_Await_Module_Validation_Before_Validated_Hook(bool temporary, bool skipValidation, bool failValidation)
    {
        using var settings = SettingsProvider.Development(new
        {
            DrnDevelopmentSettings = new { TemporaryApplication = temporary, SkipValidation = skipValidation }
        });
        var probe = new ProgramActionsProbe { PauseValidation = true, FailAsyncValidation = failValidation };
        foreach (var gate in probe.Gates)
            gate.Release.SetResult();
        var create = ProgramActionsTestProgram.CreateApplicationAsync(Arguments, settings, Substitute.For<IScopedLog>(), probe.ConfigureBuilder);
        string[] beforeValidation =
        [
            "builder:configured", "hook:0:start", "hook:0:end", "pipeline", "hook:1:start", "hook:1:end",
            "endpoints", "services:start"
        ];

        try
        {
            if (skipValidation)
            {
                await create.WaitAsync(Timeout);
                probe.ValidationGate.Entered.Task.IsCompleted.Should().BeFalse();
                probe.ValidationResolutions.Should().Be(0);
                probe.Events.Should().Equal(beforeValidation.Concat(["services:end", "hook:2:start", "hook:2:end"]));
                return;
            }

            await probe.ValidationGate.Entered.Task.WaitAsync(Timeout);
            create.IsCompleted.Should().BeFalse();
            probe.Gates[2].Entered.Task.IsCompleted.Should().BeFalse();
            probe.Events.Should().Equal(beforeValidation.Append("validation:start"));
            probe.ValidationResolutions.Should().Be(1);
            probe.ValidationGate.Release.SetResult();

            if (failValidation)
            {
                Func<Task> startup = () => create.WaitAsync(Timeout);
                var thrown = await startup.Should().ThrowExactlyAsync<InvalidOperationException>();
                thrown.Which.Should().BeSameAs(probe.Failure);
                probe.Gates[2].Entered.Task.IsCompleted.Should().BeFalse();
                probe.Events.Should().Equal(beforeValidation.Append("validation:start"));
            }
            else
            {
                await create.WaitAsync(Timeout);
                probe.Events.Should().Equal(beforeValidation.Concat(
                    ["validation:start", "validation:end", "services:end", "hook:2:start", "hook:2:end"]));
            }
        }
        finally
        {
            await CleanupAsync(probe, create);
        }
    }

    private static async Task CleanupAsync(ProgramActionsProbe probe, Task<WebApplication> create)
    {
        probe.ReleaseAll();
        try
        {
            await create.WaitAsync(Timeout);
        }
        catch (Exception exception) when (ReferenceEquals(exception, probe.Failure))
        {
            // Expected failures are asserted by the test. Dispose the captured application below.
        }
        finally
        {
            if (probe.Application != null)
                await probe.Application.DisposeAsync();
        }
    }
}
