using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.EntityFramework.Context;

public class DrnContextDevelopmentConnection
{
    const string PasswordKey = "drnPassword";
    const string Username = "postgres";
    const string DatabaseName = "drnDb";

    public static string GetConnectionString(IAppSettings appSettings, string name)
    {
        var connectionString = string.Empty;
        if (appSettings.TryGetConnectionString(name, out var devConnectionString))
            connectionString = devConnectionString;
        else
        {
            var password = appSettings.Configuration.GetValue<string>(PasswordKey);
            if (password != null)
                connectionString = $"Host=postgresql;Port=5432;Database={DatabaseName};User ID={Username};password={password};";
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return connectionString;
    }
}