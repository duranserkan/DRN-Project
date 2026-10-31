using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.SharedKernel.Attributes;

namespace DRN.Nexus.Hosted.Settings;

[Config("Identity")]
public class NexusIdentityConfig
{
    public bool RequireConfirmedAccount { get; init; } = true;
    [Sensitive(false, Justification = "Identity verification configuration flag.")]
    public bool RequireConfirmedEmail { get; init; } = true;
    [Sensitive(false, Justification = "Identity verification configuration flag.")]
    public bool RequireConfirmedPhoneNumber { get; init; } = true;
}
