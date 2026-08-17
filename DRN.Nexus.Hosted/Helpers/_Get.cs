using DRN.Framework.Hosting.Endpoints;

namespace DRN.Nexus.Hosted.Helpers;

public static class Get
{
    public static NexusEndpointFor Endpoint { get; } = (NexusEndpointFor)EndpointCollectionBase<NexusProgram>.EndpointCollection!;
}
