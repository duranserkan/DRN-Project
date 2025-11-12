using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using Microsoft.AspNetCore.Http;
using Sample.Hosted;
using Sample.Hosted.Controllers;
using Sample.Hosted.Filters;
using Sample.Hosted.Helpers;

namespace DRN.Test.Tests.Framework.Hosting.ExceptionHandler;

public class DrnExceptionModelTests
{
    [DataInline]
    [Theory]
    public async Task ErrorPageModel_Should_Be_Serialized_As_Expected(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        var errorModel = (await client.GetFromJsonAsync<DrnExceptionModel>(Get.Endpoint.Sample.Exception.GetErrorPageModel.RoutePattern))!;

        errorModel.Should().NotBeNull();
        errorModel.RequestPath.Trim('/').Should().Be(Get.Endpoint.Sample.Exception.GetErrorPageModel.RoutePattern);
    }
    
    [DataInline]
    [Theory]
    public async Task DrnExceptionFilter__Should_Be_Invoked_On_Exception(DrnTestContext context, IDrnExceptionFilter filter, ISampleDrnExceptionFilterDependency filterDependency)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        
        filter.HandlePreExceptionModelCreationAsync(Arg.Any<HttpContext>(), Arg.Any<Exception>())
            .Returns(Task.FromResult(new DrnExceptionFilterResult()));
        filter.HandleExceptionAsync(Arg.Any<HttpContext>(), Arg.Any<Exception>(), Arg.Any<DrnExceptionModel>())
            .Returns(Task.FromResult(new DrnExceptionFilterResult()));
        
        var response = await client.GetAsync(Get.Endpoint.Sample.Exception.ConflictException.RoutePattern);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await filter.Received(1).HandlePreExceptionModelCreationAsync(Arg.Any<HttpContext>(), Arg.Any<Exception>());
        await filter.Received(1).HandleExceptionAsync(Arg.Any<HttpContext>(), Arg.Any<Exception>(), Arg.Any<DrnExceptionModel>());
        filterDependency.Received(1).DoNothing();
    }
}