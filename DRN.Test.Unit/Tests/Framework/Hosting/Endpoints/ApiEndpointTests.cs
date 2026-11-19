using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel;
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

    [Fact]
    public void Signature_Binding_Should_Use_Concrete_Controller_And_Clr_Method_Not_Mvc_Name()
    {
        var read = typeof(OverloadedController).GetMethod(nameof(OverloadedController.ReadAsync), [typeof(int)])!;
        var other = typeof(OverloadedController).GetMethod(nameof(OverloadedController.ReadAsync), [typeof(string)])!;
        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(OverloadedController).GetTypeInfo(), ControllerName = "ConventionName",
            ActionName = "Renamed", MethodInfo = read
        };
        var second = new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(OverloadedController).GetTypeInfo(), ControllerName = "ConventionName",
            ActionName = "Other", MethodInfo = other
        };
        var source = new DrnEndpointSource(new DefaultEndpointDataSource(Route("/renamed/{id:int}", descriptor), Route("/other/{id}", second)));
        var parameters = new[] { typeof(int) };
        var endpoint = new ApiEndpoint(typeof(OverloadedController), "ReadAsync", parameters);
        parameters[0] = typeof(string);

        endpoint.SetEndPoint(source);

        endpoint.ActionDescriptor.Should().Equal(descriptor);
        endpoint.RoutePattern.Should().Be("/renamed/{id:int}");
    }

    private sealed class OverloadedController : ControllerBase
    {
        [SuppressMessage("Performance", "CA1822", Justification = "The binding fixture models MVC instance actions. Static methods are not MVC actions.")]
        public void ReadAsync(int id) { }
        [SuppressMessage("Performance", "CA1822", Justification = "The binding fixture models MVC instance actions. Static methods are not MVC actions.")]
        public void ReadAsync(string id) { }
    }

    private sealed class ItemsController : ControllerBase
    {
        [SuppressMessage("Performance", "CA1822", Justification = "The binding fixture models an MVC instance action. Static methods are not MVC actions.")]
        public void Read() { }
    }
}
