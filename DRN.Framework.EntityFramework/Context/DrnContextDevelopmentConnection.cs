using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.EntityFramework.Context;

public static class DrnContextDevelopmentConnection
{
    public const string PostgresDevelopmentPasswordKey = "PostgresDevelopmentPassword";
    public const string PostgresDevelopmentHostKey = "PostgresDevelopmentHost";
    public const string PostgresDevelopmentPortKey = "PostgresDevelopmentPort";
    public const string PostgresDevelopmentUsernameKey = "PostgresDevelopmentUsername";
    public const string PostgresDevelopmentDatabaseKey = "PostgresDevelopmentDatabase";
    public const string DefaultUsername = "postgres";
    public const string DefaultDatabase = "drnDb";

    public static string GetConnectionString(IAppSettings appSettings, string name)
    {
        var connectionString = string.Empty;
        if (appSettings.TryGetConnectionString(name, out var devConnectionString))
            connectionString = devConnectionString;
        else
        {
            var host = appSettings.Configuration.GetValue(PostgresDevelopmentHostKey, "postgresql");
            var port = appSettings.Configuration.GetValue(PostgresDevelopmentPortKey, 5432);
            var username = appSettings.Configuration.GetValue<string>(PostgresDevelopmentUsernameKey, DefaultUsername);
            var database = appSettings.Configuration.GetValue<string>(PostgresDevelopmentDatabaseKey, DefaultDatabase);
            var password = appSettings.Configuration.GetValue<string>(PostgresDevelopmentPasswordKey);
            if (password != null)
                connectionString = $"Host={host};Port={port};Database={database};User ID={username};password={password};";
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return connectionString;
    }
}