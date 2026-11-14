using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.SharedKernel;
using DRN.Nexus.Hosted.Controllers.Sample;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Endpoints;

public class MvcAccessorBindingTests
{
    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Binding_Should_Follow_Actual_Mvc_Discovery_Without_Changing_Clr_Identity(bool suppressAsyncSuffix)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers(options =>
        {
            options.SuppressAsyncSuffixInActionNames = suppressAsyncSuffix;
            options.Conventions.Add(new AccessorConvention());
        }).ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear();
            manager.FeatureProviders.Clear();
            manager.FeatureProviders.Add(new AccessorFeatureProvider());
        });
        using var provider = services.BuildServiceProvider();
        var actions = provider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>().ToArray();
        var routes = actions.Select(action => new RouteEndpoint(static _ => Task.CompletedTask,
            RoutePatternFactory.Parse(action.AttributeRouteInfo!.Template!), 0,
            new EndpointMetadataCollection(action), action.DisplayName)).ToArray();
        var source = new DrnEndpointSource(new DefaultEndpointDataSource(routes));

        var inherited = Bind(nameof(AccessorProbeController.ReadAsync), [typeof(Guid), typeof(CancellationToken)]);
        inherited.ActionDescriptor.Should().ContainSingle();
        inherited.ActionDescriptor[0].ActionName.Should().Be(suppressAsyncSuffix ? "Read" : "ReadAsync");
        inherited.ActionDescriptor[0].ControllerName.Should().Be("RuntimeController");
        inherited.ActionDescriptor[0].RouteValues["area"].Should().Be("Admin");
        inherited.ActionDescriptor[0].MethodInfo.DeclaringType.Should().Be(typeof(AccessorProbeBase<Guid>));

        var renamed = Bind(nameof(AccessorProbeController.RenamedAsync), []);
        renamed.ActionDescriptor[0].ActionName.Should().Be("PublicName");
        var convention = Bind(nameof(AccessorProbeController.ConventionAsync), []);
        convention.ActionDescriptor[0].ActionName.Should().Be("RuntimeAction");
        var overridden = Bind(nameof(AccessorProbeController.OverrideAsync), [typeof(Guid)]);
        overridden.ActionDescriptor[0].MethodInfo.DeclaringType.Should().Be(typeof(AccessorProbeController));

        Bind(nameof(AccessorProbeController.LookupAsync), [typeof(int)]).ActionDescriptor[0].ActionName.Should().Be("Number");
        Bind(nameof(AccessorProbeController.LookupAsync), [typeof(string)]).ActionDescriptor[0].ActionName.Should().Be("Text");
        Bind(nameof(AccessorProbeController.MultipleAsync), []).RouteEndpoints.Should().HaveCount(2);
        actions.Should().NotContain(action => action.MethodInfo.Name == nameof(AccessorProbeController.DisabledAsync));
        Action bindExcluded = () => Bind(nameof(AccessorProbeController.DisabledAsync), []);
        bindExcluded.Should().Throw<ValidationException>().WithMessage("*NonAction*");
        actions.Should().NotContain(action => action.MethodInfo.Name == nameof(AccessorProbeController.RemovedByConventionAsync));
        Action bindRemoved = () => Bind(nameof(AccessorProbeController.RemovedByConventionAsync), []);
        bindRemoved.Should().Throw<ValidationException>().WithMessage("*RemovedByConventionAsync*not discovered*");

        ApiEndpoint Bind(string method, Type[] parameters)
        {
            var endpoint = new ApiEndpoint(typeof(AccessorProbeController), method, parameters);
            endpoint.SetEndPoint(source);
            return endpoint;
        }
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Referenced_Controller_Binding_Should_Require_Mvc_Application_Part_Registration(bool registered)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear();
            if (registered)
                manager.ApplicationParts.Add(new AssemblyPart(typeof(StatusController).Assembly));
        });
        using var provider = services.BuildServiceProvider();
        var actions = provider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(StatusController)).ToArray();
        var routes = actions.Select(action => new RouteEndpoint(static _ => Task.CompletedTask,
            RoutePatternFactory.Parse(action.AttributeRouteInfo!.Template!), 0,
            new EndpointMetadataCollection(action), action.DisplayName)).ToArray();
        var source = new DrnEndpointSource(new DefaultEndpointDataSource(routes));
        var endpoint = new ApiEndpoint(typeof(StatusController), nameof(StatusController.Status), Type.EmptyTypes);

        if (registered)
        {
            actions.Should().ContainSingle();
            endpoint.SetEndPoint(source);
            endpoint.ActionDescriptor.Should().Equal(actions);
            endpoint.RoutePatterns.Should().ContainSingle().Which.TrimStart('/').Should().Be("Api/Status");
        }
        else
        {
            actions.Should().BeEmpty();
            Action bind = () => endpoint.SetEndPoint(source);
            bind.Should().Throw<ValidationException>().WithMessage("*Status.Status*application part*");
        }
    }

    private sealed class AccessorFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature) =>
            feature.Controllers.Add(typeof(AccessorProbeController).GetTypeInfo());
    }

    private sealed class AccessorConvention : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                controller.ControllerName = "RuntimeController";
                foreach (var action in controller.Actions.Where(action => action.ActionMethod.Name == nameof(AccessorProbeController.ConventionAsync)))
                    action.ActionName = "RuntimeAction";
                foreach (var action in controller.Actions.Where(action =>
                             action.ActionMethod.Name == nameof(AccessorProbeController.RemovedByConventionAsync)).ToArray())
                    controller.Actions.Remove(action);
            }
        }
    }

    public abstract class AccessorProbeBase<T> : ControllerBase
    {
        [HttpGet("read/{id}")]
        public virtual Task<T> ReadAsync(T id, CancellationToken cancellationToken = default) => Task.FromResult(id);

        [HttpGet("override/{id}")]
        public virtual Task<T> OverrideAsync(T id) => Task.FromResult(id);

        [HttpGet("disabled")]
        public virtual Task DisabledAsync() => Task.CompletedTask;
    }

    [Area("Admin")]
    [Route("accessor-probe/[action]")]
    public sealed class AccessorProbeController : AccessorProbeBase<Guid>
    {
        [HttpGet]
        [ActionName("PublicName")]
        public Task RenamedAsync() => Task.CompletedTask;

        [HttpGet]
        public Task ConventionAsync() => Task.CompletedTask;

        [HttpGet]
        public Task RemovedByConventionAsync() => Task.CompletedTask;

        public override Task<Guid> OverrideAsync(Guid id) => Task.FromResult(id);

        [NonAction]
        public sealed override Task DisabledAsync() => Task.CompletedTask;

        [HttpGet("number/{id:int}")]
        [ActionName("Number")]
        public Task<int> LookupAsync(int id) => Task.FromResult(id);

        [HttpGet("text/{id}")]
        [ActionName("Text")]
        public Task<string> LookupAsync(string id) => Task.FromResult(id);

        [HttpGet("first")]
        [HttpGet("second")]
        public Task MultipleAsync() => Task.CompletedTask;
    }
}
