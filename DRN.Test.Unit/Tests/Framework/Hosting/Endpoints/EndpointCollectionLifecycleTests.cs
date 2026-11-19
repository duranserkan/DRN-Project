using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Endpoints;

public class EndpointCollectionLifecycleTests
{
    [Fact]
    public async Task Host_Should_Bind_The_Configured_Typed_Singleton()
    {
        var route = Route("/configured/{id:guid}");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(Helper(new DefaultEndpointDataSource(route)));
        await using var application = builder.Build();
        application.UseRouting();
        var settings = Substitute.For<IAppSettings>();
        settings.DevelopmentSettings.Returns(new DrnDevelopmentSettings());
        var program = new ConfiguredCatalogProgram();

        program.Validate(application, settings);

        var collection = ConfiguredCatalog.EndpointCollection;
        collection.Should().BeSameAs(EndpointCollectionBase<ConfiguredCatalogProgram, ConfiguredCatalog>.EndpointCollection);
        EndpointCatalog<ConfiguredCatalogProgram>.ApiEndpoints.Should().ContainSingle()
            .Which.Should().BeSameAs(collection.Items.Read);
        collection.Items.Read.RouteEndpoints.Should().Equal(route);
        program.CollectionRequests.Should().Be(1);
    }

    [Fact]
    public void Catalog_Should_Allow_A_Host_Without_Typed_Accessors()
    {
        var route = Route("/untyped/{id:guid}");
        EndpointCatalog<NoCatalogProgram>.SetEndpointDataSource(Helper(new DefaultEndpointDataSource(route)), null);

        EndpointCatalog<NoCatalogProgram>.Endpoints.Should().ContainSingle().Which.Should().BeSameAs(route);
        EndpointCatalog<NoCatalogProgram>.ApiEndpoints.Should().BeEmpty();
    }

    [Fact]
    public void Discovery_Should_Skip_The_Typed_Singleton_And_Preserve_Static_Groups()
    {
        var route = Route("/discovery/{id:guid}");
        var source = Helper(new DefaultEndpointDataSource(route));

        EndpointCatalog<DiscoveryCatalogProgram>.SetEndpointDataSource(source,
            EndpointCollectionBase<DiscoveryCatalogProgram, DiscoveryCatalog>.EndpointCollection);

        DiscoveryCatalog.RootReads.Should().Be(0);
        var collection = DiscoveryCatalog.EndpointCollection;
        DiscoveryCatalog.RootReads.Should().Be(1);
        collection.Should().BeSameAs(EndpointCollectionBase<DiscoveryCatalogProgram, DiscoveryCatalog>.EndpointCollection);
        var endpoints = EndpointCatalog<DiscoveryCatalogProgram>.ApiEndpoints;
        endpoints.Should().HaveCount(2);
        endpoints.Should().Contain(collection.Items.Read).And.Contain(DiscoveryCatalog.LegacyItems.Read);
        collection.Items.Read.RouteEndpoints.Should().Equal(route);
        DiscoveryCatalog.LegacyItems.Read.RouteEndpoints.Should().Equal(route);
    }

    [Fact]
    public async Task Catalog_Should_Skip_Temporary_Validation_Retry_Failure_And_Isolate_Concurrent_Initialization()
    {
        var settings = Substitute.For<IAppSettings>();
        settings.DevelopmentSettings.Returns(new DrnDevelopmentSettings { TemporaryApplication = true });
        await using (var application = WebApplication.CreateBuilder().Build())
            new FirstCatalogProgram().ValidateTemporary(application, settings);
        EndpointCatalog<FirstCatalogProgram>.Endpoints.Should().BeEmpty();
        EndpointCatalog<FirstCatalogProgram>.ApiEndpoints.Should().BeEmpty();

        var missing = Helper(new DefaultEndpointDataSource(Array.Empty<Endpoint>()));
        Action initializeMissing = () => EndpointCatalog<FirstCatalogProgram>.SetEndpointDataSource(missing, FirstCatalog.EndpointCollection);
        initializeMissing.Should().Throw<ValidationException>().WithMessage("*Read*not discovered*");

        var firstRoute = Route("/first/{id:guid}");
        var first = Helper(new DefaultEndpointDataSource(firstRoute));
        var secondRoute = Route("/second/{id:guid}");
        var second = Helper(new DefaultEndpointDataSource(secondRoute));
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, 8).Select(index => Task.Run(() =>
        {
            start.Wait();
            if (index % 2 == 0)
                EndpointCatalog<FirstCatalogProgram>.SetEndpointDataSource(first, FirstCatalog.EndpointCollection);
            else
                EndpointCatalog<SecondCatalogProgram>.SetEndpointDataSource(second, SecondCatalog.EndpointCollection);
        })).ToArray();
        start.Set();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

        EndpointCatalog<FirstCatalogProgram>.ApiEndpoints.Should().ContainSingle()
            .Which.RouteEndpoints.Should().Equal(firstRoute);
        EndpointCatalog<SecondCatalogProgram>.ApiEndpoints.Should().ContainSingle()
            .Which.RouteEndpoints.Should().Equal(secondRoute);
        EndpointCatalog<FirstCatalogProgram>.EndpointSource.Should()
            .NotBeSameAs(EndpointCatalog<SecondCatalogProgram>.EndpointSource);

        var repeated = Helper(new DefaultEndpointDataSource(Route("/first/{id:guid}")));
        EndpointCatalog<FirstCatalogProgram>.SetEndpointDataSource(repeated, FirstCatalog.EndpointCollection);
        EndpointCatalog<FirstCatalogProgram>.ApiEndpoints.Should().ContainSingle()
            .Which.RouteEndpoints.Should().Equal(firstRoute);
    }

    private static IEndpointHelper Helper(EndpointDataSource source)
    {
        var helper = Substitute.For<IEndpointHelper>();
        helper.EndpointDataSource.Returns(source);
        return helper;
    }

    private static RouteEndpoint Route(string template)
    {
        var action = new ControllerActionDescriptor
        {
            ControllerName = "CatalogProbe",
            ActionName = "Read",
            ControllerTypeInfo = typeof(CatalogProbeController).GetTypeInfo(),
            MethodInfo = typeof(CatalogProbeController).GetMethod(nameof(CatalogProbeController.Read))!
        };
        return new RouteEndpoint(static _ => Task.CompletedTask, RoutePatternFactory.Parse(template), 0,
            new EndpointMetadataCollection(action), template);
    }

    public sealed class CatalogProbeController : ControllerBase
    {
        [SuppressMessage("Performance", "CA1822", Justification = "The endpoint collection fixture binds an MVC instance action. Static methods are not MVC actions.")]
        public void Read(Guid id) { }
    }

    public sealed class CatalogProbeFor() : ControllerForBase<CatalogProbeController>("")
    {
        [EndpointAction(nameof(CatalogProbeController.Read), typeof(Guid))]
        public ApiEndpoint Read { get; private set; } = null!;
    }

    public sealed class FirstCatalog : EndpointCollectionBase<FirstCatalogProgram, FirstCatalog>
    {
        public CatalogProbeFor Items { get; } = new();
    }

    public sealed class ConfiguredCatalog : EndpointCollectionBase<ConfiguredCatalogProgram, ConfiguredCatalog>
    {
        public CatalogProbeFor Items { get; } = new();
    }

    public sealed class SecondCatalog : EndpointCollectionBase<SecondCatalogProgram, SecondCatalog>
    {
        public CatalogProbeFor Items { get; } = new();
    }

    public sealed class DiscoveryCatalog : EndpointCollectionBase<DiscoveryCatalogProgram, DiscoveryCatalog>
    {
        internal static int RootReads;

        public new static DiscoveryCatalog EndpointCollection
        {
            get
            {
                Interlocked.Increment(ref RootReads);
                return EndpointCollectionBase<DiscoveryCatalogProgram, DiscoveryCatalog>.EndpointCollection;
            }
        }

        public CatalogProbeFor Items { get; } = new();
        public static CatalogProbeFor LegacyItems { get; } = new();
    }

    // These types isolate catalog statics. They are not application entry points used by a test host.
    public sealed class ConfiguredCatalogProgram : DrnProgramBase<ConfiguredCatalogProgram>, IDrnProgram
    {
        public int CollectionRequests { get; private set; }
        static Task IDrnProgram.Main(string[] args) => Task.CompletedTask;
        public void Validate(WebApplication application, IAppSettings settings) => ValidateEndpoints(application, settings);
        protected override EndpointCollection<ConfiguredCatalogProgram> ConfigureEndpointCollection()
        {
            CollectionRequests++;
            return ConfiguredCatalog.EndpointCollection;
        }
        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog) => Task.CompletedTask;
    }

    public sealed class NoCatalogProgram : DrnProgramBase<NoCatalogProgram>, IDrnProgram
    {
        static Task IDrnProgram.Main(string[] args) => Task.CompletedTask;
        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog) => Task.CompletedTask;
    }

    public sealed class FirstCatalogProgram : DrnProgramBase<FirstCatalogProgram>, IDrnProgram
    {
        static Task IDrnProgram.Main(string[] args) => Task.CompletedTask;
        public void ValidateTemporary(WebApplication application, IAppSettings settings) => ValidateEndpoints(application, settings);
        protected override EndpointCollection<FirstCatalogProgram> ConfigureEndpointCollection() => FirstCatalog.EndpointCollection;
        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog) => Task.CompletedTask;
    }

    public sealed class SecondCatalogProgram : DrnProgramBase<SecondCatalogProgram>, IDrnProgram
    {
        static Task IDrnProgram.Main(string[] args) => Task.CompletedTask;
        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog) => Task.CompletedTask;
    }

    public sealed class DiscoveryCatalogProgram : DrnProgramBase<DiscoveryCatalogProgram>, IDrnProgram
    {
        static Task IDrnProgram.Main(string[] args) => Task.CompletedTask;
        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog) => Task.CompletedTask;
    }
}
