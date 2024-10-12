using Microsoft.AspNetCore.Identity;
using Sample.Hosted.Auth.EndpointRouteBuilderExtensions.Endpoints.ManageGroup;

namespace Sample.Hosted.Auth.EndpointRouteBuilderExtensions.Endpoints;

/// <summary>
/// https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Core/src/IdentityApiEndpointRouteBuilderExtensions.cs
/// </summary>
public static class MapIdentityApiExtensions
{
    /// <summary>
    /// Add endpoints for registering, logging in, and logging out using ASP.NET Core Identity.
    /// </summary>
    /// <typeparam name="TUser">The type describing the user. This should match the generic parameter in <see cref="UserManager{TUser}"/>.</typeparam>
    /// <param name="endpoints">
    /// The <see cref="IEndpointRouteBuilder"/> to add the identity endpoints to.
    /// Call <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, string)"/> to add a prefix to all the endpoints.
    /// </param>
    /// <param name="prefix">
    /// The pattern that prefixes all routes in this group.
    /// </param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> to further customize the added endpoints.</returns>
    public static IEndpointConventionBuilder MapDrnIdentityApi<TUser>(this IEndpointRouteBuilder endpoints, string prefix)
        where TUser : class, new()
    {
        var routeGroup = endpoints.MapGroup(prefix);
        routeGroup.WithTags("Auth");

        routeGroup.MapIdentityApiRegister<TUser>();
        routeGroup.MapIdentityApiLogin<TUser>();
        routeGroup.MapIdentityApiRefresh<TUser>();
        routeGroup.MapIdentityApiResetPassword<TUser>();
        routeGroup.MapIdentityApiForgotPassword<TUser>();
        routeGroup.MapIdentityApiConfirmEmail<TUser>();
        routeGroup.MapIdentityApiResendConfirmationEmail<TUser>();

        var accountGroup = routeGroup.MapGroup("/manage");
        accountGroup.WithTags("Auth Management");
        accountGroup.MapIdentityApiManageEndpoints<TUser>();

        return new IdentityEndpointsConventionBuilder(routeGroup);
    }
}