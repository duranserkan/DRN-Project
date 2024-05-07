using Microsoft.AspNetCore.Builder;

namespace DRN.Framework.Hosting.DrnProgram;

public static class DrnProgramConventions
{
    public static WebApplicationBuilder GetApplicationBuilder<TProgram>(WebApplicationOptions options, DrnAppBuilderType drnAppBuilderType)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
    {
        var builder = drnAppBuilderType switch
        {
            DrnAppBuilderType.DrnDefaults => WebApplication.CreateEmptyBuilder(options),
            DrnAppBuilderType.Empty => WebApplication.CreateEmptyBuilder(options),
            DrnAppBuilderType.Slim => WebApplication.CreateSlimBuilder(options),
            DrnAppBuilderType.Default => WebApplication.CreateBuilder(options),
            _ => WebApplication.CreateEmptyBuilder(options)
        };

        return builder;
    }
}