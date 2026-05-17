using DRN.Framework.Utils.Data.Hashing;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Hosting.RateLimiting;

internal static class RateLimitPartitionRedactor
{
    internal static string Format(string? partitionKey, DrnRateLimitOptions options, IAppSecuritySettings securitySettings)
    {
        if (string.IsNullOrWhiteSpace(partitionKey))
            return string.Empty;

        return options.PartitionLogMode switch
        {
            RateLimitPartitionLogMode.PlainText => partitionKey,
            RateLimitPartitionLogMode.KeyedHash => Redact(partitionKey, securitySettings),
            _ => Redact(partitionKey, securitySettings)
        };
    }

    private static string Redact(string partitionKey, IAppSecuritySettings securitySettings)
    {
        var key = securitySettings.AppHashKey.HashToBinary();

        return $"blake3-keyed:{partitionKey.HashWithKey(key)}";
    }
}
