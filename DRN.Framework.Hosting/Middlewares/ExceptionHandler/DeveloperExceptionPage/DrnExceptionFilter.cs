// This file is licensed to you under the MIT license.

using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler;

public interface IDrnExceptionFilter
{
    Task<DrnExceptionFilterResult> HandlePreExceptionModelCreationAsync(HttpContext httpContext, Exception exception);
    Task<DrnExceptionFilterResult> HandleExceptionAsync(HttpContext httpContext, Exception exception, DrnExceptionModel model);
}

public class DrnExceptionFilterResult
{
    public static DrnExceptionFilterResult Default => new();
    public bool SkipPostCreationFilter { get; set; }
    public bool SkipExceptionHandling { get; set; }
}