using System.Net;
using Sample.Hosted;
using Sample.Hosted.Helpers;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using Microsoft.AspNetCore.Http;
using Sample.Hosted.Filters;

namespace DRN.Test.Integration.Tests.Framework.Hosting.ExceptionHandler;

public class DrnExceptionModelTests
{
    [DataInline]
    [Theory]
    public async Task Anonymous_Request_Should_Be_Denied_The_ErrorPageModel(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        context.ApplicationContext.GetCreatedApplication<SampleProgram>()!.Services
            .GetServices<IDrnExceptionFilter>().Should().BeEmpty();
        using var response = await client.GetAsync(Get.Endpoint.Sample.Exception.GetErrorPageModel.RoutePattern);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [DataInline]
    [Theory]
    public async Task DrnExceptionFilter__Should_Be_Invoked_On_Exception(DrnTestContext context, IDrnExceptionFilter filter, ISampleDrnExceptionFilterDependency filterDependency)
    {
        context.ServiceCollection.AddScoped<IDrnExceptionFilter, SampleDrnExceptionFilter>();
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
