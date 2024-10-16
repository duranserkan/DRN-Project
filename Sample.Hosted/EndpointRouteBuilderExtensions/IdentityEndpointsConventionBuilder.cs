namespace Sample.Hosted.EndpointRouteBuilderExtensions;

public sealed class IdentityEndpointsConventionBuilder(RouteGroupBuilder inner) : IEndpointConventionBuilder
{
    private IEndpointConventionBuilder InnerAsConventionBuilder => inner;

    public void Add(Action<EndpointBuilder> convention) => InnerAsConventionBuilder.Add(convention);
    public void Finally(Action<EndpointBuilder> finallyConvention) => InnerAsConventionBuilder.Finally(finallyConvention);
}