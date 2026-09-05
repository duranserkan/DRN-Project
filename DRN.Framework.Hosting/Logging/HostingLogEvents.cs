using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.Logging;

/// <summary>Stable event identifiers emitted by Hosting. Applications can define companion catalogs for their own events.</summary>
/// <remarks>IDs are unique within Hosting. Preserve published IDs and names; filter across modules by logger category and ID.</remarks>
public static class HostingLogEvents
{
    public static readonly EventId MfaAuthorizationChallenge = new(7401, nameof(MfaAuthorizationChallenge));
    public static readonly EventId MfaAuthorizationForbid = new(7402, nameof(MfaAuthorizationForbid));
    public static readonly EventId MfaAuthorizationExemption = new(7403, nameof(MfaAuthorizationExemption));
}
