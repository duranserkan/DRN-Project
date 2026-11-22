using System.Diagnostics.CodeAnalysis;
using DRN.Framework.EntityFramework.Attributes;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.EntityFramework.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DRN.Test.Unit.Tests.Framework.EntityFramework;

public class DbContextExtensionsTests
{
    [Theory]
    [DataInlineUnit("Acme.Data", "Acme.Data", true)]
    [DataInlineUnit("Acme.Data.Configurations", "Acme.Data", true)]
    [DataInlineUnit("Acme.Data.Configurations.Sub", "Acme.Data", true)]
    [DataInlineUnit("Acme.Database", "Acme.Data", false)]
    [DataInlineUnit("Acme.DataOther", "Acme.Data", false)]
    [DataInlineUnit("Other.Acme.Data", "Acme.Data", false)]
    [DataInlineUnit(null, "Acme.Data", false)]
    [DataInlineUnit("Acme.Data", null, false)]
    [DataInlineUnit(null, null, true)]
    public void IsExactOrChildNamespace_Should_Only_Accept_Exact_Or_True_Child_Namespaces(
        string? typeNamespace, string? contextNamespace, bool expected)
    {
        var result = DbContextExtensions.IsExactOrChildNamespace(typeNamespace, contextNamespace);
        result.Should().Be(expected);
    }

    [Fact]
    public void ConfigureNpgsqlDataSourceBuilder_Should_Invoke_Registered_Attributes_With_Null_ServiceProvider()
    {
        var builder = new NpgsqlDataSourceBuilder("Host=localhost;Database=test");
        builder.ConfigureNpgsqlDataSourceBuilder<TestContextWithCustomAttribute>();

        builder.ConnectionStringBuilder.ApplicationName.Should().Be("CustomApp");
        builder.ConnectionStringBuilder.CommandTimeout.Should().Be(42);
    }

    [Fact]
    public void DrnContextDefaults_ConfigureNpgsqlDataSource_Should_Fallback_To_ContextName_When_ServiceProvider_Is_Null()
    {
        var builder = new NpgsqlDataSourceBuilder("Host=localhost;Database=test");
        builder.ConfigureNpgsqlDataSourceBuilder<TestContextWithDefaults>();

        builder.ConnectionStringBuilder.ApplicationName.Should().Be(nameof(TestContextWithDefaults));
    }

    [Fact]
    public void CreateDbContext_With_ConnectionString_Should_Invoke_ConfigureNpgsqlDataSourceBuilder()
    {
        string[] args = ["Host=localhost;Database=test"];
        using var context = args.CreateDbContext<TestContextWithCustomAttribute>();

        context.Should().NotBeNull();

        var csBuilder = new NpgsqlConnectionStringBuilder(context.Database.GetDbConnection().ConnectionString);
        csBuilder.Host.Should().Be("localhost");
        csBuilder.Database.Should().Be("test");
        csBuilder.ApplicationName.Should().Be("CustomApp");
        csBuilder.CommandTimeout.Should().Be(42);
    }

    [Fact]
    public void ConfigureNpgsqlDataSourceBuilder_Should_Override_Inherited_Performance_Defaults_And_Apply_Custom_Options()
    {
        var builder = new NpgsqlDataSourceBuilder("Host=localhost;Database=test");
        builder.ConfigureNpgsqlDataSourceBuilder<DerivedOptionsContext>();

        DbContextConventions.GetContextAttributes<DerivedOptionsContext>()
            .OfType<DrnContextPerformanceDefaultsAttribute>().Should().ContainSingle();
        builder.ConnectionStringBuilder.MaxPoolSize.Should().Be(50);
        builder.ConnectionStringBuilder.ApplicationName.Should().Be("CustomApp");
        builder.ConnectionStringBuilder.CommandTimeout.Should().Be(42);
    }

    [Fact]
    [SuppressMessage("Usage", "CA2263", Justification = "Verifies both the generic and runtime Type overloads.")]
    public void GetContextAttributes_Should_Reject_Repeated_Options_Type()
    {
        var genericAct = () => DbContextConventions.GetContextAttributes<RepeatedOptionsContext>();
        var typeAct = () => DbContextConventions.GetContextAttributes(typeof(RepeatedOptionsContext));
        var inheritedAct = () => DbContextConventions.GetContextAttributes(typeof(DerivedRepeatableOptionsContext));

        genericAct.Should().Throw<ConfigurationException>().WithMessage($"*{nameof(RepeatableOptionsAttribute)}*");
        typeAct.Should().Throw<ConfigurationException>().WithMessage($"*{nameof(RepeatableOptionsAttribute)}*");
        inheritedAct.Should().Throw<ConfigurationException>().WithMessage($"*{nameof(RepeatableOptionsAttribute)}*");
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class CustomDataSourceAttribute : NpgsqlDbContextOptionsAttribute
{
    public override void ConfigureNpgsqlDataSource<TContext>(NpgsqlDataSourceBuilder builder, IServiceProvider? serviceProvider)
    {
        builder.ConnectionStringBuilder.ApplicationName = "CustomApp";
        builder.ConnectionStringBuilder.CommandTimeout = 42;
    }
}

[CustomDataSource]
public class TestContextWithCustomAttribute : DbContext
{
    public TestContextWithCustomAttribute(DbContextOptions<TestContextWithCustomAttribute> options) : base(options)
    {
    }
}

[DrnContextDefaults]
public class TestContextWithDefaults : DbContext
{
    public TestContextWithDefaults(DbContextOptions<TestContextWithDefaults> options) : base(options)
    {
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RepeatableOptionsAttribute : NpgsqlDbContextOptionsAttribute;

[DrnContextPerformanceDefaults, CustomDataSource]
public class BaseOptionsContext(DbContextOptions options) : DbContext(options);

[DrnContextPerformanceDefaults(maxPoolSize: 50)]
public class DerivedOptionsContext(DbContextOptions options) : BaseOptionsContext(options);

[RepeatableOptions, RepeatableOptions]
public class RepeatedOptionsContext(DbContextOptions options) : DbContext(options);

[RepeatableOptions]
public class BaseRepeatableOptionsContext(DbContextOptions options) : DbContext(options);

[RepeatableOptions]
public class DerivedRepeatableOptionsContext(DbContextOptions options) : BaseRepeatableOptionsContext(options);
