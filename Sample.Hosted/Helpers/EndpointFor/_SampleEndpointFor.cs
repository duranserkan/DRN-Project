using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Hosting.Endpoints;

namespace Sample.Hosted.Helpers.EndpointFor;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class SampleEndpointFor : EndpointCollectionBase<SampleProgram>
{
    public UserApiFor User { get; } = new();
    public SampleApiFor Sample { get; } = new();
    public QaApiFor QA { get; } = new();
}
