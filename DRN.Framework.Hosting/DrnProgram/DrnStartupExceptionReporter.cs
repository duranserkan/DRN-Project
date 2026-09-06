using System.Reflection;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Utils;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Hosting.DrnProgram;

internal static class DrnStartupExceptionReporter
{
    internal static async Task TryCreateReportAsync(Func<Task<WebApplicationBuilder>> createApplicationBuilder,
        Assembly programAssembly, IAppSettings appSettings, IScopedLog scopedLog, Exception exception, ILogger logger)
    {
        try
        {
            var applicationBuilder = await createApplicationBuilder();
            await using var services = applicationBuilder.Services.BuildServiceProvider();
            var isDevelopment = appSettings.IsDevelopmentEnvironment;
            var handler = services.GetService<IDrnExceptionHandler>();
            //todo send startup exception report to nexus in non-develop
            if (handler != null && isDevelopment)
            {
                var exceptionContentResult = await handler.GetStartupExceptionContentAsync(services, exception, scopedLog);
                if (exceptionContentResult != null)
                {
                    var directory = Path.GetDirectoryName(programAssembly.Location)!;
                    var reportDirectory = Path.Combine(directory, "StartupReports");
                    var wwwRootDirectory = Path.Combine(reportDirectory, "wwwroot");
                    ResourceExtractor.CopyWwwrootResourcesToDirectory(wwwRootDirectory);

                    //since the application is down, we should serve exception page scripts from somewhere else;
                    var exceptionReportContent = exceptionContentResult.Content.Replace("/_content/DRN.Framework.Hosting", wwwRootDirectory);

                    var reportPath = Path.Combine(directory, "StartupExceptionReport.html");
                    var reportUrl = $"file://{reportPath}";
                    await File.WriteAllTextAsync(reportPath, exceptionReportContent);
                    scopedLog.Add("StartupExceptionReportPath", reportUrl);
                    logger.LogError("Startup Exception Report Path: {ReportUrl}", reportUrl);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Failed to generate startup exception report");
        }
    }
}
