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
}

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
