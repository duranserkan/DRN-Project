using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Nexus.Hosted.Settings;

[Config("Identity")]
public class NexusIdentityConfig
{
    public bool RequireConfirmedAccount { get; init; } = true;
    public bool RequireConfirmedEmail { get; init; } = true;
    public bool RequireConfirmedPhoneNumber { get; init; } = true;
}
