using DRN.Framework.Hosting.Endpoints;

namespace DRN.Nexus.Hosted.Helpers.EndpointFor;

public class NexusEndpointFor : EndpointCollectionBase<NexusProgram>
{
    public UserApiFor User { get; } = new();
    public SampleApiFor Sample { get; } = new();
}
