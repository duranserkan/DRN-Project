using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Endpoints;

[SuppressMessage("ReSharper", "CoVariantArrayConversion")]
public class UrlHelperEndpointTests
{
    private static readonly string[] Areas = ["", "Admin"];

    [Theory]
    [DataInlineUnit("", "", "Admin", "/Api/Items/a%20b?q=one%26two")]
    [DataInlineUnit("/api", "", "Admin", "/api/Api/Items/a%20b?q=one%26two")]
    [DataInlineUnit("/gateway", "Admin", "", "/gateway/Admin/Api/Items/a%20b?q=one%26two")]
    public void Endpoint_Should_Generate_Urls_Through_Real_Routing(
        DrnTestContextUnit context, string pathBase, string targetArea, string ambientArea, string expected)
    {
        var routes = Areas.Select(area =>
        {
            var action = new ControllerActionDescriptor { ActionName = "Read", ControllerName = "Items" };
            action.RouteValues["area"] = area;
            var identity = new RouteValueDictionary { ["action"] = "Read", ["controller"] = "Items", ["area"] = area };
            var prefix = area.Length == 0 ? "" : $"/{area}";
            return new RouteEndpoint(static _ => Task.CompletedTask,
                RoutePatternFactory.Parse($"{prefix}/Api/Items/{{id}}", identity, parameterPolicies: null, requiredValues: identity),
                0, new EndpointMetadataCollection(action), $"Read-{area}");
        }).ToArray();
        var target = routes.Single(route => route.Metadata.GetRequiredMetadata<ControllerActionDescriptor>().RouteValues["area"] == targetArea);
        var endpoint = new ApiEndpoint(typeof(ItemsController), "ReadAsync");
        var source = new DrnEndpointSource();
        source.EndpointMap.Add(endpoint.GetEndpointKey(), [target]);
        endpoint.SetEndPoint(source);

        context.ServiceCollection.AddLogging();
        context.ServiceCollection.AddRouting();
        context.ServiceCollection.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource(routes));
        var http = new DefaultHttpContext { RequestServices = context };
        http.Request.PathBase = pathBase;
        http.SetEndpoint(target);
        var routeData = new RouteData();
        routeData.Values["area"] = ambientArea;
        var url = new UrlHelperFactory().GetUrlHelper(new ActionContext(http, routeData, new ControllerActionDescriptor()));

        url.Endpoint(endpoint, new { id = "a b", q = "one&two" }).Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Endpoint_Should_Generate_Unnamed_And_Explicitly_Named_Links_Independently(
        DrnTestContextUnit context, bool named)
    {
        var action = new ControllerActionDescriptor { ActionName = "Read", ControllerName = "Items" };
        var identity = new RouteValueDictionary { ["action"] = "Read", ["controller"] = "Items", ["area"] = "" };
        var metadata = new List<object> { action };
        if (named)
        {
            metadata.Add(new EndpointNameMetadata("Items.Read.Explicit"));
            metadata.Add(new RouteNameMetadata("Items.Read.Explicit"));
        }
        var route = new RouteEndpoint(static _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/items/{id:guid}", identity, parameterPolicies: null, requiredValues: identity),
            0, new EndpointMetadataCollection(metadata), "Read");
        var endpoint = new ApiEndpoint(typeof(ItemsController), "ReadAsync");
        var source = new DrnEndpointSource();
        source.EndpointMap.Add(endpoint.GetEndpointKey(), [route]);
        endpoint.SetEndPoint(source);
        context.ServiceCollection.AddLogging();
        context.ServiceCollection.AddRouting();
        context.ServiceCollection.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource(route));
        var http = new DefaultHttpContext { RequestServices = context };
        http.Request.PathBase = "/gateway";
        http.SetEndpoint(route);
        var url = new UrlHelperFactory().GetUrlHelper(new ActionContext(http, new RouteData(), new ControllerActionDescriptor()));
        var id = Guid.Parse("5ad30189-61f1-4b21-8fc5-97416c75bd64");
        var values = new { id, q = "one&two" };
        const string expected = "/gateway/items/5ad30189-61f1-4b21-8fc5-97416c75bd64?q=one%26two";

        url.Endpoint(endpoint, values).Should().Be(expected);
        if (named)
            url.RouteUrl("Items.Read.Explicit", values).Should().Be(expected);
        else
            route.Metadata.GetMetadata<IEndpointNameMetadata>().Should().BeNull();
        url.RouteUrl(endpoint.EndpointName, values).Should().BeNull();

        Action missing = () => url.Endpoint(endpoint);
        Action invalid = () => url.Endpoint(endpoint, new { id = "not-a-guid" });
        missing.Should().Throw<InvalidOperationException>().WithMessage("*Could not generate a URL*");
        invalid.Should().Throw<InvalidOperationException>().WithMessage("*Could not generate a URL*");
    }

    [Theory]
    [DataInlineUnit("")]
    [DataInlineUnit("Admin")]
    public void Endpoint_Should_Use_Mvc_Identity_And_Target_Area_Without_Mutating_Values(string area)
    {
        var action = new ControllerActionDescriptor { ActionName = "Read", ControllerName = "Items" };
        if (area.Length > 0) action.RouteValues["area"] = area;
        var endpoint = CreateEndpoint(action, action);
        var values = new RouteValueDictionary { ["id"] = "a b", ["q"] = "one&two", ["area"] = "Ambient" };
        var url = Substitute.For<IUrlHelper>();
        UrlActionContext? generated = null;
        url.Action(Arg.Any<UrlActionContext>()).Returns(call =>
        {
            generated = call.Arg<UrlActionContext>();
            return "/gateway/Api/Items/a%20b?q=one%26two";
        });

        var result = url.Endpoint(endpoint, values);

        result.Should().Be("/gateway/Api/Items/a%20b?q=one%26two");
        generated.Should().NotBeNull();
        generated!.Action.Should().Be("Read");
        generated.Controller.Should().Be("Items");
        var actual = generated.Values.Should().BeOfType<RouteValueDictionary>().Which;
        actual["area"].Should().Be(area);
        actual["id"].Should().Be("a b");
        actual["q"].Should().Be("one&two");
        values["area"].Should().Be("Ambient");
    }

    [Fact]
    public void Endpoint_Should_Reject_Uninitialized_Metadata()
    {
        var url = Substitute.For<IUrlHelper>();
        var endpoint = new ApiEndpoint(typeof(ItemsController), "ReadAsync");

        Action generate = () => url.Endpoint(endpoint);

        generate.Should().Throw<InvalidOperationException>().WithMessage("*no initialized MVC action metadata*");
        url.DidNotReceive().Action(Arg.Any<UrlActionContext>());
    }

    [Fact]
    public void Endpoint_Should_Reject_Ambiguous_Actions()
    {
        var endpoint = CreateEndpoint(
            new ControllerActionDescriptor { ActionName = "Read", ControllerName = "Items" },
            new ControllerActionDescriptor { ActionName = "Other", ControllerName = "Items" });
        var url = Substitute.For<IUrlHelper>();

        Action generate = () => url.Endpoint(endpoint);

        generate.Should().Throw<InvalidOperationException>().WithMessage("*multiple MVC actions*");
    }

    [Fact]
    public void Endpoint_Should_Reject_Failed_Url_Generation()
    {
        var endpoint = CreateEndpoint(new ControllerActionDescriptor { ActionName = "Read", ControllerName = "Items" });
        var url = Substitute.For<IUrlHelper>();
        url.Action(Arg.Any<UrlActionContext>()).Returns((string?)null);

        Action generate = () => url.Endpoint(endpoint);

        generate.Should().Throw<InvalidOperationException>().WithMessage("*Could not generate a URL*");
    }

    private static ApiEndpoint CreateEndpoint(params ControllerActionDescriptor[] actions)
    {
        var endpoint = new ApiEndpoint(typeof(ItemsController), "ReadAsync");
        var source = new DrnEndpointSource();
        source.EndpointMap.Add(endpoint.GetEndpointKey(), actions.Select(action =>
            new RouteEndpoint(static _ => Task.CompletedTask, RoutePatternFactory.Parse("/Api/Items/{id}"), 0,
                new EndpointMetadataCollection(action), "Read")).ToArray());
        endpoint.SetEndPoint(source);
        return endpoint;
    }

    private sealed class ItemsController : ControllerBase { }
}
