using System.Threading.RateLimiting;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Hosting.RateLimiting;

internal static class RateLimitTokenBucketOptions
{
    internal static TokenBucketRateLimiterOptions CreatePreAuth(DrnRateLimitOptions options) =>
        Create(
            GetOverrideOrDefault(options.PreAuthTokenLimit, options.TokenLimit),
            GetOverrideOrDefault(options.PreAuthReplenishmentSeconds, options.ReplenishmentSeconds),
            GetOverrideOrDefault(options.PreAuthTokensPerPeriod, options.TokensPerPeriod));

    internal static TokenBucketRateLimiterOptions CreatePostAuth(DrnRateLimitOptions options) =>
        Create(
            GetOverrideOrDefault(options.PostAuthTokenLimit, options.TokenLimit),
            GetOverrideOrDefault(options.PostAuthReplenishmentSeconds, options.ReplenishmentSeconds),
            GetOverrideOrDefault(options.PostAuthTokensPerPeriod, options.TokensPerPeriod));

    private static TokenBucketRateLimiterOptions Create(int tokenLimit, int replenishmentSeconds, int tokensPerPeriod) =>
        new()
        {
            TokenLimit = tokenLimit,
            ReplenishmentPeriod = TimeSpan.FromSeconds(replenishmentSeconds),
            TokensPerPeriod = tokensPerPeriod,
            QueueLimit = 0,
            AutoReplenishment = true
        };

    private static int GetOverrideOrDefault(int overrideValue, int defaultValue) =>
        overrideValue > 0 ? overrideValue : defaultValue;
}
