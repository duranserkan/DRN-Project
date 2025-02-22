using DRN.Framework.Hosting.Endpoints;
using DRN.Nexus.Hosted.Controllers;

namespace DRN.Nexus.Hosted.Helpers;

public static class Get
{
    public static NexusEndpointFor Endpoint { get; } = (NexusEndpointFor)EndpointCollectionBase<NexusProgram>.EndpointCollection!;
}