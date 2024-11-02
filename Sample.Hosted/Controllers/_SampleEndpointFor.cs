using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Controllers;

public abstract class SampleEndpointFor : EndpointCollectionBase<SampleProgram>
{
    public static UserApiFor User { get; } = new();
    public static SampleApiFor Sample { get; } = new();
}