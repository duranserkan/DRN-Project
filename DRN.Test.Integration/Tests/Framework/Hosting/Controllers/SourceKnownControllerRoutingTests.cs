using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Test.Integration.Helpers;
using DRN.Test.Utils.Hosting;
using DRN.Test.Utils.Hosting.ApiErrors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;

namespace DRN.Test.Integration.Tests.Framework.Hosting.Controllers;

public class SourceKnownControllerRoutingTests
{
    [Theory]
    [IsolatedHostingData]
    public async Task Combined_Controller_Should_Require_Auth_Remove_NonActions_And_Retain_Hidden_Actions(
        DrnTestContext context)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        context.AddToConfiguration("Environment", "Production");
        context.AddToConfiguration("AllowedHosts", "localhost");
        var repository = Substitute.For<ISourceKnownRepository<SourceKnownRoutingEntity>>();
        repository.AnyAsync().Returns(true);
        context.ServiceCollection.AddSingleton(repository);
        var application = context.ApplicationContext.CreateApplication<ApiErrorTestProgram>();
        using var client = application.CreateClient(new() { AllowAutoRedirect = false });

        using var anonymous = await client.GetAsync("/source-known-routing/Any");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var tokenResponse = await client.GetAsync(ApiErrorTestProgram.Root + "/token");
        tokenResponse.EnsureSuccessStatusCode();
        using var tokenBody = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenBody.RootElement.GetProperty("access-token").GetString());

        using var hidden = await client.GetAsync("/source-known-routing/Any");
        hidden.StatusCode.Should().Be(HttpStatusCode.OK);
        (await hidden.Content.ReadFromJsonAsync<bool>()).Should().BeTrue();
        using var disabledRead = await client.GetAsync("/source-known-routing/Count");
        disabledRead.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var disabledWrite = await client.DeleteAsync($"/source-known-routing/{Guid.NewGuid()}");
        disabledWrite.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        using var createWithoutWritePolicy = await client.PostAsJsonAsync("/source-known-routing", new SourceKnownRoutingRequest("probe"));
        createWithoutWritePolicy.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await repository.DidNotReceive().CreateAsync(Arg.Any<IReadOnlyCollection<SourceKnownRoutingEntity>>());
        await repository.DidNotReceive().DeleteAsync(Arg.Any<IReadOnlyCollection<Guid>>());

        var descriptions = application.Services
            .GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Where(description => description.ActionDescriptor is ControllerActionDescriptor controllerAction &&
                                  controllerAction.ControllerTypeInfo.AsType() == typeof(SourceKnownRoutingController))
            .ToArray();
        descriptions.Should().NotContain(description => ((ControllerActionDescriptor)description.ActionDescriptor).MethodInfo.Name == "AnyAsync");
        descriptions.Should().NotContain(description => ((ControllerActionDescriptor)description.ActionDescriptor).MethodInfo.Name == "CountAsync");
        descriptions.Should().NotContain(description => ((ControllerActionDescriptor)description.ActionDescriptor).MethodInfo.Name == "DeleteAsync");
        descriptions.Should().Contain(description => description.HttpMethod == "POST" && description.RelativePath == "source-known-routing");
        descriptions.Should().Contain(description => description.HttpMethod == "GET" && description.RelativePath == "source-known-routing");
    }

    [Theory]
    [IsolatedHostingData]
    public async Task Bearer_Writes_Should_Require_Policy_And_Resource_Authorization_Without_Antiforgery(DrnTestContext context)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        context.AddToConfiguration("Environment", "Production");
        context.AddToConfiguration("AllowedHosts", "localhost");
        var repository = Substitute.For<ISourceKnownRepository<SourceKnownRoutingEntity>>();
        context.ServiceCollection.AddSingleton(repository);
        var application = context.ApplicationContext.CreateApplication<ApiErrorTestProgram>();
        using var client = application.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AuthenticateAsync(client);
        var id = Guid.NewGuid();

        using var policyDeniedCreate = await client.PostAsJsonAsync("/source-known-writes", new SourceKnownRoutingRequest("allowed"));
        using var policyDeniedDelete = await client.DeleteAsync($"/source-known-writes/{id}");
        policyDeniedCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        policyDeniedDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await repository.DidNotReceive().CreateAsync(Arg.Any<IReadOnlyCollection<SourceKnownRoutingEntity>>());
        await repository.DidNotReceive().DeleteAsync(Arg.Any<IReadOnlyCollection<Guid>>());

        await AuthenticateAsync(client, allowWrites: true);
        using var deniedCreate = await client.PostAsJsonAsync("/source-known-writes", new SourceKnownRoutingRequest("denied"));
        using var deniedDelete = await client.DeleteAsync($"/source-known-writes/{Guid.Empty}");
        deniedCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deniedDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await repository.DidNotReceive().CreateAsync(Arg.Any<IReadOnlyCollection<SourceKnownRoutingEntity>>());
        await repository.DidNotReceive().DeleteAsync(Arg.Any<IReadOnlyCollection<Guid>>());

        using var created = await client.PostAsJsonAsync("/source-known-writes", new SourceKnownRoutingRequest("allowed"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("label").GetString().Should().Be("allowed");
        body.RootElement.GetProperty("id").ValueKind.Should().Be(JsonValueKind.String);
        body.RootElement.TryGetProperty("entity_id_source", out _).Should().BeFalse();
        await repository.Received(1).CreateAsync(Arg.Is<IReadOnlyCollection<SourceKnownRoutingEntity>>(entities => entities.Single().Label == "allowed"));

        using var deleted = await client.DeleteAsync($"/source-known-writes/{id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await deleted.Content.ReadAsStringAsync()).Should().BeEmpty();
        await repository.Received(1).DeleteAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == id));
    }

    [Theory]
    [IsolatedHostingData]
    public async Task SourceKnown_Policies_Should_Reject_Valid_Cookies_Even_When_Browser_Is_The_Default_Scheme(DrnTestContext context)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        context.AddToConfiguration("Environment", "Production");
        context.AddToConfiguration("AllowedHosts", "localhost");
        var repository = Substitute.For<ISourceKnownRepository<SourceKnownRoutingEntity>>();
        context.ServiceCollection.AddSingleton(repository);
        context.ServiceCollection.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultScheme = "Browser";
            options.DefaultAuthenticateScheme = "Browser";
            options.DefaultChallengeScheme = "Browser";
        });
        var application = context.ApplicationContext.CreateApplication<ApiErrorTestProgram>();
        using var client = application.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        application.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value.DefaultAuthenticateScheme.Should().Be("Browser");

        using var signedIn = await client.GetAsync(ApiErrorTestProgram.Root + "/cookie-sign-in");
        signedIn.EnsureSuccessStatusCode();
        using var cookieProbe = await client.GetAsync(ApiErrorTestProgram.Root + "/cookie-protected");
        cookieProbe.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cookieProbe.Content.ReadAsStringAsync()).Should().Be("cookie-authenticated");
        client.DefaultRequestHeaders.Authorization.Should().BeNull();

        using var read = await client.GetAsync("/source-known-writes/Any");
        using var create = await client.PostAsJsonAsync("/source-known-writes", new SourceKnownRoutingRequest("allowed"));
        using var delete = await client.DeleteAsync($"/source-known-writes/{Guid.NewGuid()}");
        read.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        create.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        delete.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        repository.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [IsolatedHostingData]
    public async Task Pagination_Should_Bind_Query_And_Json_And_Normalize_Null_Nested_Models(DrnTestContext context)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        context.AddToConfiguration("Environment", "Production");
        context.AddToConfiguration("AllowedHosts", "localhost");
        var repository = Substitute.For<ISourceKnownRepository<SourceKnownRoutingEntity>>();
        var requests = new List<PaginationRequest>();
        repository.PaginateAsync(Arg.Any<PaginationRequest>(), null).Returns(call =>
        {
            var request = call.ArgAt<PaginationRequest>(0);
            requests.Add(request);
            var items = Enumerable.Range(0, request.PageSize.Size + 1)
                .Select(index => new SourceKnownRoutingEntity { Label = $"item-{index}" }).ToArray();
            return new PaginationResult<SourceKnownRoutingEntity>(items, request).ToModel();
        });
        context.ServiceCollection.AddSingleton(repository);
        var application = context.ApplicationContext.CreateApplication<ApiErrorTestProgram>();
        using var client = application.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AuthenticateAsync(client);

        using var query = await client.GetAsync("/source-known-writes?PageSize.Size=1000&PageSize.MaxSize=1000&TotalCount=500");
        query.StatusCode.Should().Be(HttpStatusCode.OK);
        using var queryBody = JsonDocument.Parse(await query.Content.ReadAsStringAsync());
        queryBody.RootElement.GetProperty("items").GetArrayLength().Should().Be(100);
        queryBody.RootElement.GetProperty("info").GetProperty("has_next").GetBoolean().Should().BeTrue();

        using var options = await client.GetAsync("/source-known-writes/Paginate?PageSize=1000&MaxSize=1000");
        options.StatusCode.Should().Be(HttpStatusCode.OK);

        using var nullContent = new StringContent("""{"page_size":null,"page_cursor":null}""", Encoding.UTF8, "application/json");
        using var nullResponse = await client.PostAsync("/source-known-writes/Paginate", nullContent);
        nullResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var nullBody = JsonDocument.Parse(await nullResponse.Content.ReadAsStringAsync());
        nullBody.RootElement.GetProperty("items").GetArrayLength().Should().Be(10);

        var refresh = new PaginationRequest(2, new PageSize(2), new PageCursor(2, Guid.NewGuid(), Guid.NewGuid()),
            totalCount: 500, markAsHasNextOnRefresh: true);
        using var refreshContent = JsonContent.Create(refresh, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        using var refreshResponse = await client.PostAsync("/source-known-writes/Paginate", refreshContent);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var refreshBody = JsonDocument.Parse(await refreshResponse.Content.ReadAsStringAsync());
        refreshBody.RootElement.GetProperty("items").GetArrayLength().Should().Be(2);
        refreshBody.RootElement.GetProperty("info").GetProperty("has_next").GetBoolean().Should().BeTrue();

        requests.Should().HaveCount(4);
        requests.Select(request => request.PageSize.Size).Should().Equal(100, 100, 10, 2);
        requests.Should().OnlyContain(request => request.TotalCount == -1 && !request.MarkAsHasNextOnRefresh);
        requests[2].PageCursor.IsFirstRequest.Should().BeTrue();
        requests[3].IsPageRefresh.Should().BeTrue();
    }

    private static async Task AuthenticateAsync(HttpClient client, bool allowWrites = false)
    {
        using var response = await client.GetAsync(ApiErrorTestProgram.Root + (allowWrites ? "/write-token" : "/token"));
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("access-token").GetString());
    }
}
