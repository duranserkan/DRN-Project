using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Controllers;

public class SampleEndpointFor : EndpointCollectionBase<SampleProgram>
{
    public UserApiFor User { get; } = new();
    public SampleApiFor Sample { get; } = new();
    public QaApiFor QA { get; } = new();
}