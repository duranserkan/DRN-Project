using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace DRN.Test.Integration.Tests.Sample.Pages.Helpers;

internal sealed class PathBaseStartupFilter(string pathBase) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        if (pathBase.Length > 0)
            app.UsePathBase(pathBase);
        next(app);
    };
}
