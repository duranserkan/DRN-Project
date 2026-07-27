using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DRN.Framework.EntityFramework.Context;

public static class DrnContextDevelopmentConnection
{
    public static string GetConnectionString(IAppSettings appSettings, string name)
    {
        if (!appSettings.IsDevelopmentEnvironment)
            throw ExceptionFor.Configuration($"Development connection helper cannot be used outside development environment. Current environment: '{appSettings.Environment}'.");

        var connectionString = string.Empty;
        if (appSettings.TryGetConnectionString(name, out var devConnectionString))
            connectionString = devConnectionString;
        else
        {
            var host = appSettings.Configuration.GetValue(DbContextConventions.DevHostKey, DbContextConventions.DefaultHost);
            var port = appSettings.Configuration.GetValue<int>(DbContextConventions.DevPortKey, DbContextConventions.DefaultPort);
            var username = appSettings.Configuration.GetValue<string>(DbContextConventions.DevUsernameKey, DbContextConventions.DefaultUsername);
            var database = appSettings.Configuration.GetValue<string>(DbContextConventions.DevDatabaseKey, DbContextConventions.DefaultDatabase);
            var password = appSettings.Configuration.GetValue<string>(DbContextConventions.DevPasswordKey);

            if (password != null)
            {
                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = host,
                    Port = port,
                    Database = database,
                    Username = username,
                    Password = password,
                    MaxAutoPrepare = 10,
                    MaxPoolSize = 20,
                    ApplicationName = AppConstants.EntryAssemblyName
                };
                connectionString = builder.ConnectionString;
            }
        }

        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        throw ExceptionFor.Configuration(
            $"Connection string for '{name}' not found. Ensure the app is compiled in debug mode when using Postgres in Dev Environment with Test Containers.");
    }
}
