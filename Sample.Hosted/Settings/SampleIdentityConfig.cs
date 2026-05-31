using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace Sample.Hosted.Settings;

[Config("Identity")]
public class SampleIdentityConfig
{
    public bool RequireConfirmedAccount { get; init; } = true;
    public bool RequireConfirmedEmail { get; init; } = true;
    public bool RequireConfirmedPhoneNumber { get; init; } = true;
}
