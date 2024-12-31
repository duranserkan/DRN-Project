using System.Reflection;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetEscapades.AspNetCore.SecurityHeaders;

namespace DRN.Framework.Hosting.Endpoints;

public interface IPageUtils
{
    Task<string> RenderPageAsync<TModel>(string pagePath, TModel model, HttpContext? parentContext = null) where TModel : PageModel;
}

[Transient<IPageUtils>]
public class PageUtils(
    ITempDataProvider tempDataProvider,
    IRazorViewEngine viewEngine,
    IEndpointAccessor endpointAccessor,
    IModelMetadataProvider modelMetadataProvider,
    IOptions<FormOptions> formOptions,
    IServiceScopeFactory serviceScopeFactory)
    : IPageUtils
{
    public async Task<string> RenderPageAsync<TModel>(string pagePath, TModel model, HttpContext? parentContext = null) where TModel : PageModel
    {
        var viewResult = viewEngine.GetView(null, pagePath, true);
        if (viewResult.View == null)
            throw new InvalidOperationException($"The Razor page at '{pagePath}' could not be found.");

        var context = new DefaultHttpContext();
        context.RequestServices = serviceScopeFactory.CreateScope().ServiceProvider;
        context.FormOptions = formOptions.Value;
        context.ServiceScopeFactory = serviceScopeFactory;
        context.Initialize(context.Features);
        if (parentContext != null)
            context.Items["NETESCAPADES_NONCE"] = parentContext.GetNonce();
        try
        {
            var razorView = (RazorView)viewResult.View;
            var razorPage = (Page)razorView.RazorPage;
            var pageEndpoint = endpointAccessor.PageEndpointByPaths[pagePath];
            var actionContext = new ActionContext(context, context.GetRouteData(), pageEndpoint.ActionDescriptor);
            model.PageContext = new PageContext(actionContext);
            model.PageContext.ViewData = new ViewDataDictionary<TModel>(new ViewDataDictionary(modelMetadataProvider, actionContext.ModelState), model);
            razorPage.PageContext = model.PageContext;

            await using var writer = new StringWriter();
            var tempDictionary = new TempDataDictionary(context, tempDataProvider);
            var viewContext = new ViewContext(actionContext, razorView, model.PageContext.ViewData, tempDictionary, writer, new HtmlHelperOptions());
            viewContext.HttpContext = context;

            razorView.GetType().GetProperty("OnAfterPageActivated", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(razorView, (IRazorPage rp, ViewContext vc) =>
                {
                    var p = (Page)rp;
                    _ = p;
                });

            await razorView.RenderAsync(viewContext);

            var response = writer.ToString();

            return response;
        }
        catch (Exception e)
        {
            _ = e;
            throw;
        }
        finally
        {
            context.Uninitialize();
        }
    }
}