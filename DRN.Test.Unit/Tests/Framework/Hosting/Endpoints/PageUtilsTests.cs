using System.Diagnostics;
using System.Text.Encodings.Web;
using DRN.Framework.Hosting.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Endpoints;

public class PageUtilsTests
{
    [Theory]
    [DataInlineUnit(false, "")]
    [DataInlineUnit(true, "")]
    [DataInlineUnit(true, "/gateway")]
    public async Task Rendering_Should_Generate_Endpoint_Links_With_The_Parent_PathBase(bool withParent, string pathBase)
    {
        const string pagePath = "/Pages/ScopeProbe.cshtml";
        var endpoint = new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/ScopeProbe"), 0,
            new EndpointMetadataCollection(new PageActionDescriptor { RelativePath = pagePath },
                new RouteNameMetadata("scope-probe")), "ScopeProbe");
        var services = new ServiceCollection();
        services.AddLogging().AddMvc();
        services.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource(endpoint));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var parent = withParent ? new DefaultHttpContext() : null;
        var parentEndpoint = new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "Parent");
        if (parent != null)
        {
            parent.Request.PathBase = pathBase;
            parent.SetEndpoint(parentEndpoint);
        }

        var page = new ProbePage(async razorPage =>
        {
            var viewContext = razorPage.ViewContext;
            var urlHelper = viewContext.HttpContext.RequestServices.GetRequiredService<IUrlHelperFactory>()
                .GetUrlHelper(viewContext);
            await razorPage.Output.WriteAsync(urlHelper.RouteUrl("scope-probe"));
        }) { Path = pagePath };
        var viewEngine = Substitute.For<IRazorViewEngine>();
        using var diagnostics = new DiagnosticListener(nameof(PageUtilsTests));
        var view = new RazorView(viewEngine, Substitute.For<IRazorPageActivator>(), [], page, HtmlEncoder.Default, diagnostics);
        viewEngine.GetView(null, pagePath, true).Returns(ViewEngineResult.Found(pagePath, view));
        var accessor = Substitute.For<IEndpointAccessor>();
        accessor.PageEndpointByPaths.Returns(new Dictionary<string, PageEndpoint> { [pagePath] = new([endpoint]) });
        var pageUtils = new PageUtils(Substitute.For<ITempDataProvider>(), viewEngine, accessor,
            new EmptyModelMetadataProvider(), Options.Create(new FormOptions()), provider.GetRequiredService<IServiceScopeFactory>());

        (await pageUtils.RenderPageAsync(pagePath, new ProbePageModel(), parent)).Should().Be(pathBase + "/ScopeProbe");
        if (parent != null)
        {
            parent.GetEndpoint().Should().BeSameAs(parentEndpoint);
            parent.Request.PathBase.Value.Should().Be(pathBase);
        }
    }

    [Theory]
    [DataInlineUnit(false, false)]
    [DataInlineUnit(false, true)]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(true, true)]
    public async Task Rendering_Should_Support_Layouts_And_Await_Child_Scope_Disposal(bool withLayout, bool renderingFails)
    {
        const string pagePath = "/Pages/ScopeProbe.cshtml";
        const string viewStartPath = "/Pages/_ViewStart.cshtml";
        const string layoutPath = "/Pages/Shared/_ProbeLayout.cshtml";
        var services = new ServiceCollection();
        services.AddLogging().AddMvc();
        services.AddScoped<SyncDisposable>(_ => new SyncDisposable());
        services.AddScoped<AsyncDisposable>(_ => new AsyncDisposable());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var parentScope = provider.CreateAsyncScope();
        var parentSync = parentScope.ServiceProvider.GetRequiredService<SyncDisposable>();
        var parentAsync = parentScope.ServiceProvider.GetRequiredService<AsyncDisposable>();
        var parent = new DefaultHttpContext { RequestServices = parentScope.ServiceProvider };
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Rendering failed.");
        SyncDisposable? childSync = null;
        AsyncDisposable? childAsync = null;
        var page = new ProbePage(async razorPage =>
        {
            var requestServices = razorPage.ViewContext.HttpContext.RequestServices;
            childSync = requestServices.GetRequiredService<SyncDisposable>();
            childAsync = requestServices.GetRequiredService<AsyncDisposable>();
            childAsync.DisposalGate = release.Task;
            if (renderingFails)
                throw failure;
            await razorPage.Output.WriteAsync("Rendered page");
        }) { Path = pagePath };
        var viewEngine = Substitute.For<IRazorViewEngine>();
        var viewStart = new ProbeViewStart(layoutPath) { Path = viewStartPath };
        var layout = new ProbeLayout { Path = layoutPath };
        viewEngine.GetAbsolutePath(viewStartPath, layoutPath).Returns(layoutPath);
        viewEngine.GetPage(pagePath, layoutPath).Returns(new RazorPageResult(layoutPath, layout));
        using var diagnostics = new DiagnosticListener(nameof(PageUtilsTests));
        var view = new RazorView(viewEngine, Substitute.For<IRazorPageActivator>(), withLayout ? [viewStart] : [],
            page, HtmlEncoder.Default, diagnostics);
        viewEngine.GetView(null, pagePath, true).Returns(ViewEngineResult.Found(pagePath, view));
        var accessor = Substitute.For<IEndpointAccessor>();
        var endpoint = new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/ScopeProbe"), 0,
            new EndpointMetadataCollection(new PageActionDescriptor { RelativePath = pagePath }), "ScopeProbe");
        accessor.PageEndpointByPaths.Returns(new Dictionary<string, PageEndpoint>
        {
            [pagePath] = new([endpoint])
        });
        var pageUtils = new PageUtils(Substitute.For<ITempDataProvider>(), viewEngine, accessor,
            new EmptyModelMetadataProvider(), Options.Create(new FormOptions()), provider.GetRequiredService<IServiceScopeFactory>());

        var renderTask = pageUtils.RenderPageAsync(pagePath, new ProbePageModel(), parent);
        bool completedBeforeCleanup;
        try
        {
            childAsync.Should().NotBeNull();
            await Task.WhenAny(renderTask, childAsync.DisposalStarted.Task).WaitAsync(TimeSpan.FromSeconds(5));
            completedBeforeCleanup = renderTask.IsCompleted;
        }
        finally
        {
            release.TrySetResult();
        }

        if (renderingFails)
        {
            Func<Task> render = () => renderTask.WaitAsync(TimeSpan.FromSeconds(5));
            (await render.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        }
        else
            (await renderTask.WaitAsync(TimeSpan.FromSeconds(5))).Should()
                .Be(withLayout ? "<main>Rendered page</main>" : "Rendered page");

        viewStart.Executed.Should().Be(withLayout);
        completedBeforeCleanup.Should().BeFalse("rendering must await asynchronous scope cleanup");
        childSync.Should().NotBeNull().And.NotBeSameAs(parentSync);
        childSync.DisposeCount.Should().Be(1);
        childAsync.Should().NotBeSameAs(parentAsync);
        childAsync.DisposeCount.Should().Be(1);
        childAsync.Disposed.Should().BeTrue();
        parentSync.DisposeCount.Should().Be(0);
        parentAsync.DisposeCount.Should().Be(0);
        parent.RequestServices.GetRequiredService<SyncDisposable>().Should().BeSameAs(parentSync);
    }

    private sealed class ProbePage(Func<ProbePage, Task> render) : Page
    {
        public override Task ExecuteAsync() => render(this);
    }

    private sealed class ProbePageModel : PageModel { }

    private sealed class ProbeViewStart(string layoutPath) : RazorPage
    {
        public bool Executed { get; private set; }

        public override Task ExecuteAsync()
        {
            Layout = layoutPath;
            Executed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class ProbeLayout : RazorPage
    {
        public override Task ExecuteAsync()
        {
            WriteLiteral("<main>");
            Write(RenderBody());
            WriteLiteral("</main>");
            return Task.CompletedTask;
        }
    }

    private sealed class SyncDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    private sealed class AsyncDisposable : IAsyncDisposable
    {
        public TaskCompletionSource DisposalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task DisposalGate { get; set; } = Task.CompletedTask;
        public int DisposeCount { get; private set; }
        public bool Disposed { get; private set; }

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            DisposalStarted.TrySetResult();
            await DisposalGate;
            Disposed = true;
        }
    }
}
