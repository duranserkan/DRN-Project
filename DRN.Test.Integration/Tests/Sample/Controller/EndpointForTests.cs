using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Sample.Contract.QA.Categories;
using Sample.Contract.QA.Tags;
using Sample.Hosted.Controllers.QA;

namespace DRN.Test.Integration.Tests.Sample.Controller;

public class SampleEndpointForTests
{
    private static readonly string[] CreateHttpMethods = ["POST"];

    [Theory]
    [DataInline]
    public async Task EndPointFor_Should_Return_Endpoint_Address(DrnTestContext context)
    {
        var application = await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<SampleProgram>();
        var accessor = application.Services.GetRequiredService<IEndpointAccessor>();

        // Inventory actual registrations before comparing generation modes. Accessor labels are not registrations.
        var routes = accessor.Endpoints.OfType<RouteEndpoint>().ToArray();
        foreach (var route in routes)
        {
            var action = route.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (action == null) continue;
            action.RouteValues.TryGetValue("area", out var area);
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"{action.ControllerTypeInfo.FullName}.{action.MethodInfo} | action={action.ActionName} | " +
                $"area={area} | pattern={route.RoutePattern.RawText} | order={route.Order} | " +
                $"methods={string.Join(',', route.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])} | " +
                $"routeName={route.Metadata.GetMetadata<IRouteNameMetadata>()?.RouteName} | " +
                $"endpointName={route.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName} | " +
                $"suppressLink={route.Metadata.GetMetadata<ISuppressLinkGenerationMetadata>()?.SuppressLinkGeneration}");
        }

        var confirmEmailEndpoint = Get.Endpoint.User.Identity.RegisterController.ConfirmEmail;
        confirmEmailEndpoint.RoutePattern.Should().NotBeNull();

        var confirmationEndpoints = Get.Endpoint.User.Identity.RegisterController.Endpoints;
        confirmationEndpoints.Should().HaveCountGreaterThan(0);

        var http = new DefaultHttpContext
        {
            RequestServices = application.Services,
            Request =
            {
                PathBase = "/gateway"
            }
        };
        http.SetEndpoint(confirmEmailEndpoint.RouteEndpoints[0]);
        var ambient = new RouteData
        {
            Values =
            {
                ["area"] = "UnrelatedArea"
            }
        };
        var url = new UrlHelperFactory().GetUrlHelper(new ActionContext(http, ambient, new ControllerActionDescriptor()));
        var values = new ConfirmationRouteValues("test user", "one&two");
        var generated = url.Endpoint(confirmEmailEndpoint, values);
        generated.Should().Be("/gateway/Api/User/SampleIdentityRegister/ConfirmEmail?userId=test%20user&code=one%26two");

        var namedRoutes = confirmEmailEndpoint.RouteEndpoints
            .Where(route => route.RoutePattern.RawText == confirmEmailEndpoint.RoutePattern)
            .Where(route => route.Metadata.GetMetadata<ISuppressLinkGenerationMetadata>()?.SuppressLinkGeneration != true)
            .Select(route => route.Metadata.GetMetadata<IRouteNameMetadata>()?.RouteName)
            .Where(name => name != null).Distinct().ToArray();
        foreach (var name in namedRoutes)
            url.RouteUrl(name, values).Should().Be(generated);
        if (namedRoutes.Length == 0)
            url.RouteUrl(confirmEmailEndpoint.EndpointName, values).Should().BeNull();

        AssertSourceKnownActions<TagController>(routes, Get.Endpoint.QA.Tag.Endpoints, typeof(TagPostRequest),
            "DeleteAsync", "GetAsync", "PaginateAsync", "PaginateWithBodyAsync", "PaginateWithOptionsAsync", "PostAsync");
        AssertSourceKnownActions<CategoryController>(routes, Get.Endpoint.QA.Category.Endpoints, typeof(CategoryPostRequest),
            "GetAsync", "PaginateAsync", "PaginateWithBodyAsync", "PaginateWithOptionsAsync", "PostAsync");
    }

    private sealed record ConfirmationRouteValues(string userId, [property: Sensitive] string code);

    private static void AssertSourceKnownActions<TController>(RouteEndpoint[] routes, ApiEndpoint[] accessors,
        Type createRequest, params string[] expected)
    {
        var actions = routes.Select(route => route.Metadata.GetMetadata<ControllerActionDescriptor>())
            .Where(action => action?.ControllerTypeInfo.AsType() == typeof(TController))
            .Select(action => action!.MethodInfo.Name).Distinct().Order(StringComparer.Ordinal);
        actions.Should().Equal(expected);
        accessors.Select(endpoint => endpoint.ActionMethodName).Order(StringComparer.Ordinal).Should().Equal(expected);
        accessors.Should().OnlyContain(endpoint => endpoint.RouteEndpoints.Length > 0);
        foreach (var endpoint in accessors)
        {
            endpoint.ActionDescriptor.Should().OnlyContain(action => action.ControllerTypeInfo.AsType() == typeof(TController));
            endpoint.ActionDescriptor.Should().OnlyContain(action =>
                action.MethodInfo.GetParameters().Last().ParameterType == typeof(CancellationToken));
        }
        var create = accessors.Single(endpoint => endpoint.ActionMethodName == "PostAsync");
        create.ActionDescriptor.Should().OnlyContain(action => action.MethodInfo.GetParameters()[0].ParameterType == createRequest);
        create.RouteEndpoints.Should().OnlyContain(route =>
            route.Metadata.GetRequiredMetadata<IHttpMethodMetadata>().HttpMethods.SequenceEqual(CreateHttpMethods));
    }
}
