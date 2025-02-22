using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace Sample.Hosted.Filters;

public interface ISampleDrnExceptionFilterDependency
{
    void DoNothingForPreExceptionModelCreation();
    void DoNothing();
}

[Transient<ISampleDrnExceptionFilterDependency>]
public class SampleDrnExceptionFilterDependency : ISampleDrnExceptionFilterDependency
{
    public void DoNothingForPreExceptionModelCreation()
    {
    }

    public void DoNothing()
    {
    }
}

[Scoped<IDrnExceptionFilter>(false)]
public class SampleDrnExceptionFilter(ISampleDrnExceptionFilterDependency filterDependency) : IDrnExceptionFilter
{
    public Task<DrnExceptionFilterResult> HandlePreExceptionModelCreationAsync(HttpContext httpContext, Exception exception)
    {
        filterDependency.DoNothingForPreExceptionModelCreation();
        return Task.FromResult(new DrnExceptionFilterResult());
    }

    public Task<DrnExceptionFilterResult> HandleExceptionAsync(HttpContext httpContext, Exception exception, DrnExceptionModel model)
    {
        filterDependency.DoNothing();

        return Task.FromResult(new DrnExceptionFilterResult());
    }
}