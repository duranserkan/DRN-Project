using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NSubstitute;

namespace DRN.Test.Unit.Tests.Framework.EntityFramework;

public class DrnContextDevelopmentConnectionTests
{
    [Fact]
    public void GetConnectionString_Outside_Development_Environment_Should_Throw_ConfigurationException()
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(false);
        appSettings.Environment.Returns(AppEnvironment.Production);

        var action = () => DrnContextDevelopmentConnection.GetConnectionString(appSettings, "TestContext");

        action.Should().Throw<ConfigurationException>()
            .WithMessage("*outside development environment*");
    }

    [Fact]
    public void GetConnectionString_When_Explicit_ConnectionString_Exists_Should_Return_It()
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(true);
        appSettings.Environment.Returns(AppEnvironment.Development);

        var expectedCs = "Host=localhost;Database=ExplicitDb;";
        string outCs = expectedCs;
        appSettings.TryGetConnectionString("TestContext", out Arg.Any<string>()!)
            .Returns(x =>
            {
                x[1] = expectedCs;
                return true;
            });

        var result = DrnContextDevelopmentConnection.GetConnectionString(appSettings, "TestContext");

        result.Should().Be(expectedCs);
    }

    [Fact]
    public void GetConnectionString_With_Dev_Password_Should_Use_NpgsqlConnectionStringBuilder_And_Escape_Special_Chars()
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(true);
        appSettings.Environment.Returns(AppEnvironment.Development);

        string? outCs = null;
        appSettings.TryGetConnectionString("TestContext", out Arg.Any<string>()!).Returns(false);

        var rawSpecialPassword = "P@ss;word=With'Quotes\"And;Semicolons!";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { DbContextConventions.DevHostKey, "127.0.0.1" },
                { DbContextConventions.DevPortKey, "5432" },
                { DbContextConventions.DevUsernameKey, "dev_user" },
                { DbContextConventions.DevDatabaseKey, "dev_db" },
                { DbContextConventions.DevPasswordKey, rawSpecialPassword }
            })
            .Build();

        appSettings.Configuration.Returns(configuration);

        var result = DrnContextDevelopmentConnection.GetConnectionString(appSettings, "TestContext");

        var builder = new NpgsqlConnectionStringBuilder(result);
        builder.Host.Should().Be("127.0.0.1");
        builder.Port.Should().Be(5432);
        builder.Username.Should().Be("dev_user");
        builder.Database.Should().Be("dev_db");
        builder.Password.Should().Be(rawSpecialPassword);
    }

    [Fact]
    public void GetConnectionString_When_No_Password_Or_ConnectionString_Should_Throw_ConfigurationException()
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(true);
        appSettings.Environment.Returns(AppEnvironment.Development);

        appSettings.TryGetConnectionString("TestContext", out Arg.Any<string>()!).Returns(false);
        appSettings.Configuration.Returns(new ConfigurationBuilder().Build());

        var action = () => DrnContextDevelopmentConnection.GetConnectionString(appSettings, "TestContext");

        action.Should().Throw<ConfigurationException>()
            .WithMessage("*Connection string for 'TestContext' not found*");
    }
}
