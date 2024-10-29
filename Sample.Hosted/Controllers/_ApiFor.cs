using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Controllers;

public abstract class ApiFor : EndpointCollectionBase<Program>
{
    public static UserApiFor User { get; } = new();
    public static SampleApiFor Sample { get; } = new();
}