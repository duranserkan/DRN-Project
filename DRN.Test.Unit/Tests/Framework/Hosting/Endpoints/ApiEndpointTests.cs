using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Endpoints;

public class ApiEndpointTests
{
    [Theory]
    [DataInlineUnit("/items/{id:guid}", "{id:guid}")]
    [DataInlineUnit("/items/{itemId}", "{itemId}")]
    public void Path_Should_Honor_The_Selected_Legacy_Placeholder(string pattern, string placeholder)
    {
        var endpoint = new ApiEndpoint(typeof(ItemsController), nameof(ItemsController.Read));
        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = "Items", ActionName = "Read", ControllerTypeInfo = typeof(ItemsController).GetTypeInfo(),
            MethodInfo = typeof(ItemsController).GetMethod(nameof(ItemsController.Read))!
        };
        var route = Route(pattern, descriptor);
        var source = new DrnEndpointSource(new DefaultEndpointDataSource(route));
        endpoint.SetEndPoint(source);
        var id = Guid.Parse("5ad30189-61f1-4b21-8fc5-97416c75bd64");

        endpoint.Path(id, placeholder).Should().Be("/items/5ad3018961f14b218fc597416c75bd64");
        if (placeholder == "{id:guid}")
            endpoint.Path(id).Should().Be("/items/5ad3018961f14b218fc597416c75bd64");
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Missing_Binding_Should_Explain_Discovery_Failure(bool emptyRegistration)
    {
        var endpoint = new ApiEndpoint(typeof(ItemsController), nameof(ItemsController.Read));
        var source = new DrnEndpointSource();
        if (emptyRegistration)
            source.EndpointMap.Add(endpoint.GetEndpointKey(), []);

        Action bind = () => endpoint.SetEndPoint(source);

        bind.Should().Throw<ValidationException>().WithMessage("*Items.Read*application part*NonAction*");
    }

    [Fact]
    public void Source_Should_Preserve_Multiple_Routes_For_The_Same_Descriptor()
    {
        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = "Items", ActionName = "Read", ControllerTypeInfo = typeof(ItemsController).GetTypeInfo(),
            MethodInfo = typeof(ItemsController).GetMethod(nameof(ItemsController.Read))!
        };
        var first = Route("/items/{id}", descriptor);
        var second = Route("/legacy/items/{id}", descriptor);
        var source = new DrnEndpointSource(new DefaultEndpointDataSource(first, second));
        var endpoint = new ApiEndpoint(typeof(ItemsController), nameof(ItemsController.Read));

        endpoint.SetEndPoint(source);

        source.EndpointMap[descriptor.GetEndpointKey()].Should().Equal(first, second);
        endpoint.RouteEndpoints.Should().Equal(first, second);
        endpoint.RoutePatterns.Should().Equal("/items/{id}", "/legacy/items/{id}");
        source.EndpointDescriptorMap[descriptor].Should().BeSameAs(first);
    }

    private static RouteEndpoint Route(string pattern, ControllerActionDescriptor descriptor) =>
        new(static _ => Task.CompletedTask, RoutePatternFactory.Parse(pattern), 0,
            new EndpointMetadataCollection(descriptor), pattern);

    private sealed class ItemsController : ControllerBase
    {
        public void Read() { }
    }
}
