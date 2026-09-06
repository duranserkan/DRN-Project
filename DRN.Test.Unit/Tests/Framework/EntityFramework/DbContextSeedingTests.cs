using DRN.Framework.EntityFramework.Attributes;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.Utils.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Test.Unit.Tests.Framework.EntityFramework;

public class DbContextSeedingTests
{
    [Theory]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(false, true)]
    [DataInlineUnit(true, true)]
    [DataInlineUnit(false, false)]
    public async Task Repeated_Configuration_Should_Seed_Once_With_Current_Provider(bool customSync, bool customAsync)
    {
        var originalProbe = new SeedProbe();
        using var originalProvider = CreateProvider(originalProbe);
        var currentProbe = new SeedProbe();
        using var currentProvider = CreateProvider(currentProbe);
        var customEvents = new List<string>();
        var builder = new DbContextOptionsBuilder<SeedContext>();
        if (customSync)
            builder.UseSeeding((_, _) => customEvents.Add("sync"));
        if (customAsync)
            builder.UseAsyncSeeding((_, _, _) =>
            {
                customEvents.Add("async");
                return Task.CompletedTask;
            });

        DbContextConventions.UpdateDbContextOptionsBuilder<SeedContext>(builder, originalProvider);
        // Options may be copied into another builder before conventions are applied again.
        builder = new DbContextOptionsBuilder<SeedContext>(builder.Options);
        DbContextConventions.UpdateDbContextOptionsBuilder<SeedContext>(builder, currentProvider);
        DbContextConventions.UpdateDbContextOptionsBuilder<SeedContext>(builder, currentProvider);
        using var context = new SeedContext(builder.Options);
        var callbacks = builder.Options.FindExtension<CoreOptionsExtension>()!;

        callbacks.Seeder!(context, false);
        await callbacks.AsyncSeeder!(context, false, CancellationToken.None);

        originalProbe.Events.Should().BeEmpty();
        currentProbe.Events.Should().Equal("attribute", "attribute");
        string[] expected = customSync && customAsync ? ["sync", "async"]
            : customSync ? ["sync", "sync"] : customAsync ? ["async", "async"] : [];
        customEvents.Should().Equal(expected);

        DbContextConventions.UpdateDbContextOptionsBuilder<SeedContext>(builder);
        var restored = builder.Options.FindExtension<CoreOptionsExtension>()!;
        if (customSync)
            restored.Seeder!(context, false);
        else
            restored.Seeder.Should().BeNull();
        if (customAsync)
            await restored.AsyncSeeder!(context, false, CancellationToken.None);
        else
            restored.AsyncSeeder.Should().BeNull();
        currentProbe.Events.Should().Equal("attribute", "attribute");
        customEvents.Count.Should().Be(expected.Length + (customSync ? 1 : 0) + (customAsync ? 1 : 0));
    }

    [Fact]
    public async Task Async_Seeding_Should_Preserve_Custom_Callback_And_Retry_Without_Migrations()
    {
        var probe = new SeedProbe { Fail = true };
        using var provider = CreateProvider(probe);
        var builder = new DbContextOptionsBuilder<SeedContext>();
        builder.UseAsyncSeeding((_, changed, _) =>
        {
            changed.Should().BeFalse();
            probe.Events.Add("custom");
            return Task.CompletedTask;
        });
        DbContextConventions.UpdateDbContextOptionsBuilder<SeedContext>(builder, provider);
        using var context = new SeedContext(builder.Options);
        var callback = builder.Options.FindExtension<CoreOptionsExtension>()!.AsyncSeeder!;

        var firstAttempt = () => callback(context, false, CancellationToken.None);
        await firstAttempt.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage("seed failed");

        probe.Fail = false;
        await callback(context, false, CancellationToken.None);

        probe.Events.Should().Equal("custom", "attribute", "custom", "attribute");
    }

    [Fact]
    public void Synchronous_Seeding_Should_Preserve_Custom_Callback_And_Run_Attribute()
    {
        var probe = new SeedProbe();
        using var provider = CreateProvider(probe);
        var builder = new DbContextOptionsBuilder<SeedContext>();
        builder.UseSeeding((_, _) => probe.Events.Add("custom"));
        DbContextConventions.UpdateDbContextOptionsBuilder<SeedContext>(builder, provider);
        using var context = new SeedContext(builder.Options);

        builder.Options.FindExtension<CoreOptionsExtension>()!.Seeder!(context, false);

        probe.Events.Should().Equal("custom", "attribute");
    }

    [Fact]
    public async Task Cancelled_Seeding_Should_Not_Invoke_Attribute()
    {
        var probe = new SeedProbe();
        using var provider = CreateProvider(probe);
        var builder = new DbContextOptionsBuilder<SeedContext>();
        DbContextConventions.UpdateDbContextOptionsBuilder<SeedContext>(builder, provider);
        using var context = new SeedContext(builder.Options);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => builder.Options.FindExtension<CoreOptionsExtension>()!.AsyncSeeder!(context, false, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        probe.Events.Should().BeEmpty();
    }

    [Fact]
    public void Design_Time_Options_Without_Provider_Should_Not_Register_Attribute_Seeding()
    {
        var builder = new DbContextOptionsBuilder<SeedContext>();
        DbContextConventions.UpdateDbContextOptionsBuilder<SeedContext>(builder);

        var coreOptions = builder.Options.FindExtension<CoreOptionsExtension>() ?? new CoreOptionsExtension();
        coreOptions.Seeder.Should().BeNull();
        coreOptions.AsyncSeeder.Should().BeNull();
    }

    [Fact]
    public async Task Async_Initialization_Should_Not_Skip_A_Synchronous_Custom_Seed()
    {
        var probe = new SeedProbe();
        using var provider = CreateProvider(probe);
        var builder = new DbContextOptionsBuilder<SeedContext>();
        builder.UseSeeding((_, _) => probe.Events.Add("custom"));
        DbContextConventions.UpdateDbContextOptionsBuilder<SeedContext>(builder, provider);
        using var context = new SeedContext(builder.Options);

        await builder.Options.FindExtension<CoreOptionsExtension>()!.AsyncSeeder!(context, false, CancellationToken.None);

        probe.Events.Should().Equal("custom", "attribute");
    }

    [Fact]
    public void Synchronous_Initialization_Should_Not_Skip_An_Asynchronous_Custom_Seed()
    {
        var probe = new SeedProbe();
        using var provider = CreateProvider(probe);
        var builder = new DbContextOptionsBuilder<SeedContext>();
        builder.UseAsyncSeeding((_, _, _) =>
        {
            probe.Events.Add("custom");
            return Task.CompletedTask;
        });
        DbContextConventions.UpdateDbContextOptionsBuilder<SeedContext>(builder, provider);
        using var context = new SeedContext(builder.Options);

        builder.Options.FindExtension<CoreOptionsExtension>()!.Seeder!(context, false);

        probe.Events.Should().Equal("custom", "attribute");
    }

    [Theory]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(true, true)]
    [DataInlineUnit(false, false)]
    [DataInlineUnit(false, true)]
    public async Task Startup_Should_Enter_Migrator_Whenever_Enabled(bool migrate, bool pending)
    {
        var migrator = Substitute.For<IMigrator>();
        using var efProvider = new ServiceCollection()
            .AddEntityFrameworkNpgsql()
            .AddScoped<IMigrator>(_ => migrator)
            .BuildServiceProvider();
        var options = new DbContextOptionsBuilder<SeedContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseInternalServiceProvider(efProvider).Options;
        using var context = new SeedContext(options);
        using var provider = CreateProvider(new SeedProbe());
        var flags = new DbContextChangeModelFlags(false, false, false) { Migrate = migrate };
        var model = new DbContextChangeModel(nameof(SeedContext), ["Migration1"], pending ? [] : ["Migration1"], flags);

        await DrnContextServiceRegistrationHelper.ProcessChangeModelAsync(
            context, provider, provider.GetRequiredService<IAppSettings>(), model, null);

        await migrator.Received(migrate ? 1 : 0).MigrateAsync(null, CancellationToken.None);
        provider.GetRequiredService<SeedProbe>().Events.Should().BeEmpty("the helper must let EF invoke seeding under its lock");
    }

    [Theory]
    [DataInlineUnit(false, false)]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(true, true)]
    public async Task Prototype_Should_Only_Create_And_Seed_After_Any_Required_Deletion(bool exists, bool hasTables)
    {
        var operations = new List<string>();
        var creator = Substitute.For<IRelationalDatabaseCreator>();
        creator.ExistsAsync(CancellationToken.None).Returns(Task.FromResult(exists));
        creator.HasTablesAsync(CancellationToken.None).Returns(Task.FromResult(hasTables));
        creator.EnsureDeletedAsync(CancellationToken.None).Returns(_ =>
        {
            operations.Add("delete");
            return Task.FromResult(true);
        });
        creator.EnsureCreatedAsync(CancellationToken.None).Returns(_ =>
        {
            operations.Add("create and seed");
            return Task.FromResult(true);
        });
        using var efProvider = new ServiceCollection()
            .AddEntityFrameworkNpgsql()
            .AddScoped<IDatabaseCreator>(_ => creator)
            .AddScoped<IRelationalDatabaseCreator>(_ => creator)
            .BuildServiceProvider();
        var options = new DbContextOptionsBuilder<SeedContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseInternalServiceProvider(efProvider).Options;
        using var context = new SeedContext(options);
        using var provider = CreateProvider(new SeedProbe());
        var flags = new DbContextChangeModelFlags(true, true, false)
        {
            Migrate = true,
            DevelopmentSettingsPrototypeFlag = true
        };
        var model = new DbContextChangeModel(nameof(SeedContext), [], [], flags);

        await DrnContextServiceRegistrationHelper.ProcessChangeModelAsync(
            context, provider, provider.GetRequiredService<IAppSettings>(), model, null);

        string[] expected = exists && hasTables ? ["delete", "create and seed"] : ["create and seed"];
        operations.Should().Equal(expected);
        provider.GetRequiredService<SeedProbe>().Events.Should().BeEmpty("EF creation owns the seed callback");
    }

    private static ServiceProvider CreateProvider(SeedProbe probe) => new ServiceCollection()
        .AddSingleton(Substitute.For<IAppSettings>())
        .AddSingleton(probe)
        .BuildServiceProvider();

    private sealed class SeedProbe
    {
        public List<string> Events { get; } = [];
        public bool Fail { get; set; }
    }

    private sealed class SeedOptionsAttribute : NpgsqlDbContextOptionsAttribute
    {
        public override Task SeedAsync(IServiceProvider serviceProvider, IAppSettings appSettings)
        {
            var probe = serviceProvider.GetRequiredService<SeedProbe>();
            probe.Events.Add("attribute");
            return probe.Fail ? Task.FromException(new InvalidOperationException("seed failed")) : Task.CompletedTask;
        }
    }

    [SeedOptions]
    private sealed class SeedContext(DbContextOptions<SeedContext> options) : DbContext(options);
}
