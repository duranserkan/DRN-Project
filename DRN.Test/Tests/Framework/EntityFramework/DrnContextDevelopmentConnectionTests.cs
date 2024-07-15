using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Testing.Contexts.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sample.Infra;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Framework.EntityFramework;

public class DrnContextDevelopmentConnectionTests
{
    [Theory]
    [DataInline(AppEnvironment.Development, "ViveLaRépublique", true)]
    [DataInline(AppEnvironment.Production, "ViveLaRépublique", true)]
    public async Task ConnectionString_Should_Be_Created(TestContext testContext, AppEnvironment environment, string password, bool migrate)
    {
        var containerSettings = new PostgresContainerSettings
        {
            Password = password
        };

        var container = await testContext.ContainerContext.Postgres.Isolated.StartAsync(containerSettings);
        var csBuilder = new NpgsqlConnectionStringBuilder(container.GetConnectionString());

        var developmentDbSettings = new Dictionary<string, object>
        {
            { nameof(AppSettings.Environment), environment },
            { DbContextConventions.DevPasswordKey, password },
            { DbContextConventions.DevHostKey, csBuilder.Host! },
            { DbContextConventions.DevPortKey, csBuilder.Port },
            { DrnAppFeatures.GetKey(nameof(DrnAppFeatures.AutoMigrateDevEnvironment)), migrate }
        };

        testContext.AddToConfiguration(developmentDbSettings);
        testContext.ServiceCollection.AddSampleInfraServices();

        var appSettings = testContext.GetRequiredService<IAppSettings>();
        appSettings.GetValue<string>(DbContextConventions.DevPasswordKey).Should().Be(password);
        appSettings.Features.AutoMigrateDevEnvironment.Should().BeTrue();

        var connectionString = DrnContextDevelopmentConnection.GetConnectionString(appSettings, nameof(QAContext));
        connectionString.Should().NotBeNull();

        if (environment != AppEnvironment.Development)
        {
            var serviceProviderValidation = testContext.ValidateServices;
            //connection strings are not auto-generated other than development environment
            serviceProviderValidation.Should().Throw<ConfigurationException>();
            return;
        }

        //trigger PostStartupValidation
        testContext.ValidateServices();
        var qaContext = testContext.GetRequiredService<QAContext>();
        var migrations = (await qaContext.Database.GetAppliedMigrationsAsync()).ToArray();
        migrations.Length.Should().BePositive();
    }
}