using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Utils.Data.Encodings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sample.Infra;
using Sample.Infra.QA;

namespace DRN.Test.Integration.Tests.Framework.EntityFramework;

public class DrnContextDevelopmentConnectionTests
{
    [Theory]
    [DataInline(AppEnvironment.Development, "ViveLaRépublique", true)]
    [DataInline(AppEnvironment.Production, "ViveLaRépublique", true)]
    public async Task ConnectionString_Should_Be_Created(DrnTestContext testContext, AppEnvironment environment, string password, bool migrate)
    {
        var host = DbContextConventions.DefaultHost;
        var port = DbContextConventions.DefaultPort;
        if (environment == AppEnvironment.Development)
        {
            var containerSettings = new PostgresContainerSettings { Password = password };
            var container = await testContext.ContainerContext.Postgres.Isolated.StartAsync(containerSettings);
            var csBuilder = new NpgsqlConnectionStringBuilder(container.GetConnectionString());
            host = csBuilder.Host!;
            port = csBuilder.Port;
        }

        var developmentDbSettings = new Dictionary<string, object>
        {
            { nameof(AppSettings.Environment), environment },
            { DbContextConventions.DevPasswordKey, password },
            { DbContextConventions.DevHostKey, host },
            { DbContextConventions.DevPortKey, port },
            { DrnDevelopmentSettings.GetKey(nameof(DrnDevelopmentSettings.AutoMigrateDevelopment)), migrate }
        };

        if (environment != AppEnvironment.Development)
        {
            developmentDbSettings.Add("DrnAppFeatures:SeedKey", "Our true mentor in life is science! - Mustafa Kemal Atatürk (1924)");
            developmentDbSettings.Add("NexusAppSettings:Keys:0:KeyMaterial", new string('A', 32));
            developmentDbSettings.Add("NexusAppSettings:Keys:0:Format", nameof(ByteEncoding.Utf8));
            developmentDbSettings.Add("NexusAppSettings:Keys:0:Default", true);
        }

        testContext.AddToConfiguration(developmentDbSettings);
        testContext.ServiceCollection.AddSampleInfraServices();

        var appSettings = testContext.GetRequiredService<IAppSettings>();
        appSettings.GetValue<string>(DbContextConventions.DevPasswordKey).Should().Be(password);
        appSettings.DevelopmentSettings.AutoMigrateDevelopment.Should().Be(migrate);

        if (environment != AppEnvironment.Development)
        {
            var action = () => DrnContextDevelopmentConnection.GetConnectionString(appSettings, nameof(QAContext));
            action.Should().Throw<ConfigurationException>();

            var serviceProviderValidation = testContext.ValidateServicesAsync;
            //connection strings are not auto-generated other than development environment
            await serviceProviderValidation.Should().ThrowAsync<ConfigurationException>();
            return;
        }

        var connectionString = DrnContextDevelopmentConnection.GetConnectionString(appSettings, nameof(QAContext));
        connectionString.Should().NotBeNull();

        //trigger PostStartupValidation
        await testContext.ValidateServicesAsync();
        var qaContext = testContext.GetRequiredService<QAContext>();
        var migrations = (await qaContext.Database.GetAppliedMigrationsAsync()).ToArray();
        migrations.Length.Should().BePositive();
    }
}
