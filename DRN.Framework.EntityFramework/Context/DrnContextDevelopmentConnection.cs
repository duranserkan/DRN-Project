using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.EntityFramework.Context;

public static class DrnContextDevelopmentConnection
{
    public static string GetConnectionString(IAppSettings appSettings, string name)
    {
        var connectionString = string.Empty;
        if (appSettings.TryGetConnectionString(name, out var devConnectionString))
            connectionString = devConnectionString;
        else
        {
            var host = appSettings.Configuration.GetValue(DbContextConventions.DevHostKey, DbContextConventions.DefaultHost);
            var port = appSettings.Configuration.GetValue(DbContextConventions.DevPortKey, DbContextConventions.DefaultPort);
            var username = appSettings.Configuration.GetValue<string>(DbContextConventions.DevUsernameKey, DbContextConventions.DefaultUsername);
            var database = appSettings.Configuration.GetValue<string>(DbContextConventions.DevDatabaseKey, DbContextConventions.DefaultDatabase);
            var password = appSettings.Configuration.GetValue<string>(DbContextConventions.DevPasswordKey);

            if (password != null)
                connectionString = $"Host={host};Port={port};Database={database};User ID={username};password={password};Multiplexing=true;Max Auto Prepare=200;Maximum Pool Size=20;Application Name={AppConstants.ApplicationName};";
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return connectionString;
    }
}