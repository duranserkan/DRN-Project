using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Utils.Data.Serialization;
using DRN.Framework.Utils.Ids;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;
using DRN.Test.Integration.Tests.Sample.Pages.Helpers;
using Flurl;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Categories;
using Sample.Domain.QA.Tags;
using Sample.Hosted;
using Sample.Hosted.Controllers.QA;
using Sample.Hosted.Helpers;
using Sample.Hosted.Settings;
using Sample.Infra.QA;
using Swashbuckle.AspNetCore.Swagger;

namespace DRN.Test.Integration.Tests.Sample.Controller.QA;

public class CategoryControllerTests
{
    [Theory]
    [DataInline("")]
    [DataInline("/gateway")]
    public async Task Category_Should_Expose_Creation_Problem_And_Bounded_Pagination_Contracts(
        DrnTestContext context, string pathBase)
    {
        context.AddToConfiguration("DrnAppFeatures:DrnRateLimit:Disabled", "true");
        var application = context.ApplicationContext.CreateApplicationCore<SampleProgram>(
            TestContext.Current.TestOutputHelper, builder => builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new PathBaseStartupFilter(pathBase))));
        await context.ContainerContext.BindExternalDependenciesAsync();
        application.Server.PreserveExecutionContext = true;
        using var client = application.CreateClient(new() { AllowAutoRedirect = false });
        var policies = application.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policies.GetPolicyAsync(IdentitySettings.QaApiPolicy);
        policy.Should().NotBeNull();
        policy!.AuthenticationSchemes.Should().Equal(IdentityConstants.BearerScheme);

        var collectionPath = pathBase + "/" + Get.Endpoint.QA.Category.PaginateAsync.Path().TrimStart('/');
        collectionPath.Should().Be(pathBase + "/Api/QA/Category");
        var document = application.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var parameters = document.Paths["/Api/QA/Category"].Operations![HttpMethod.Get].Parameters!;
        parameters.Select(p => p.Name).Should().Contain(["PageSize.Size", "PageCursor.LastId"]);
        parameters.Should().NotContain(p => p.Name != null && p.Name.StartsWith("Total."));
        parameters.Where(p => p.In == Microsoft.OpenApi.ParameterLocation.Query).Should().OnlyContain(p => !p.Required);
        foreach (var name in new[] { "PageCursor.FirstId", "PageCursor.LastId" })
            parameters.Single(p => p.Name == name).Description.Should().Contain("PageCursor.PageNumber exceeds 1")
                .And.Contain("PageCursor.LastId is a nonzero UUID").And.Contain("both IDs are required");

        var tagQuery = document.Paths["/Api/QA/Tag"].Operations![HttpMethod.Get];
        var tagParameters = tagQuery.Parameters!;
        tagParameters.Select(parameter => parameter.Name).Should().Contain(["PageSize.Size", "PageCursor.FirstId", "PageCursor.LastId"]);
        tagParameters.Should().NotContain(parameter => parameter.Name != null &&
            (parameter.Name.Contains("Request.Total.") || parameter.Name.EndsWith("CountSpecified") ||
             parameter.Name.EndsWith("IsFirstRequest") || parameter.Name.EndsWith("PageDifference")));
        foreach (var name in new[] { "PageSize.Size", "PageCursor.FirstId", "PageCursor.LastId" })
            tagParameters.Single(parameter => parameter.Name == name).Required.Should().BeFalse();
        var tagBody = document.Paths["/Api/QA/Tag/Paginate"].Operations![HttpMethod.Post];
        document.Paths["/Api/QA/Tag/Paginate"].Operations.Should().ContainKey(HttpMethod.Get);
        tagBody.RequestBody!.Content.Should().ContainKey("application/json");
        document.Components!.Schemas![nameof(PageCursor)].Required.Should().Contain(["pageNumber", "firstId", "lastId", "sortDirection"]);
        document.Paths["/Api/QA/Category/{id}"].Operations.Should().NotContainKey(HttpMethod.Delete);
        foreach (var controller in new[] { "Tag", "Category" })
            foreach (var action in new[] { "Lookup", "Any", "Count" })
                document.Paths.Should().NotContainKey($"/Api/QA/{controller}/{action}");
        foreach (var operation in new[] { tagQuery, tagBody })
        {
            operation.Responses!["200"].Content.Should().ContainKey("application/json");
            operation.Responses["400"].Content.Should().ContainKey("application/problem+json");
        }
        using var unauthorized = await client.GetAsync(collectionPath);
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(client);

        var createPath = pathBase + "/" + Get.Endpoint.QA.Category.PostAsync.Path().TrimStart('/');
        using var created = await client.PostAsJsonAsync(createPath,
            new CategoryPostRequest { Name = "Contract category" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, "response body: {0}", await created.Content.ReadAsStringAsync());
        var expected = await created.Content.ReadFromJsonAsync<CategoryDto>();
        expected.Should().NotBeNull();
        expected!.Id.Should().NotBeEmpty();
        created.Headers.Location.Should().BeNull();
        var location = pathBase + "/" + Get.Endpoint.QA.Category.GetAsync.Path(expected.Id).TrimStart('/');
        var actual = await client.GetFromJsonAsync<CategoryDto>(location);
        actual.Should().BeEquivalentTo(expected);
        using var disabledDelete = await client.DeleteAsync(location);
        disabledDelete.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        using var scope = application.Services.CreateScope();
        var entityIds = scope.ServiceProvider.GetRequiredService<ISourceKnownEntityIdUtils>();
        var categoryId = entityIds.Parse(expected.Id);
        var wrongTypeId = entityIds.Generate<Tag>(categoryId.Source.Id).EntityId;
        var absentId = entityIds.Generate<Category>().EntityId;
        foreach (var missingId in new[] { Guid.NewGuid(), absentId, wrongTypeId })
        {
            var missingPath = pathBase + "/" + Get.Endpoint.QA.Category.GetAsync.Path(missingId).TrimStart('/');
            using var missing = await client.GetAsync(missingPath);
            var expectedStatus = missingId == absentId ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest;
            missing.StatusCode.Should().Be(expectedStatus);
            missing.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            using var problem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
            problem.RootElement.GetProperty("type").GetString().Should().Be(
                Microsoft.AspNetCore.Http.TypedResults.Problem(statusCode: (int)expectedStatus).ProblemDetails.Type);
            problem.RootElement.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);
            problem.RootElement.GetProperty("requestId").GetString().Should().NotBeNullOrWhiteSpace();
            problem.RootElement.TryGetProperty("stackTrace", out _).Should().BeFalse();
        }

        // Leave more than a full page after the first bounded request to exercise the cursor ceiling too.
        var database = scope.ServiceProvider.GetRequiredService<QAContext>();
        database.Categories.AddRange(Enumerable.Range(0, 201).Select(i => new Category($"Pagination category {i}")));
        await database.SaveChangesAsync();

        var first = await client.GetFromJsonAsync<PaginationResultModel<CategoryDto>>(collectionPath);
        first.Should().NotBeNull();
        first!.Items.Should().HaveCount(10);
        first.Info.Request.PageSize.MaxSize.Should().Be(100);
        first.Info.Request.UpdateTotalCount.Should().BeFalse();
        first.Info.Total.Count.Should().Be(-1);

        var minimumUrl = collectionPath.AppendQueryParam("request.PageSize.Size", value: "0");
        minimumUrl.Query.Should().Be("request.PageSize.Size=0");
        using var minimumResponse = await client.GetAsync(minimumUrl);
        var minimumBody = await minimumResponse.Content.ReadAsStringAsync();
        minimumResponse.StatusCode.Should().Be(HttpStatusCode.OK, "response body: {0}", minimumBody);
        var minimum = await minimumResponse.Content.ReadFromJsonAsync<PaginationResultModel<CategoryDto>>();
        minimum.Should().NotBeNull();
        minimum!.Items.Should().HaveCount(1);
        minimum.Info.Request.PageSize.Size.Should().Be(1);

        var nextRequest = first.Info.Request.GetNextPage(first.Info.FirstId, first.Info.LastId);
        var nextUrl = collectionPath.AppendQueryParam(nextRequest.Serialize(SerializationMethod.QueryString));
        var second = await client.GetFromJsonAsync<PaginationResultModel<CategoryDto>>(nextUrl);
        second.Should().NotBeNull();
        second!.Items.Should().HaveCount(10);
        second.Info.Request.PageNumber.Should().Be(2);
        second.Items.Select(item => item.Id).Intersect(first.Items.Select(item => item.Id)).Should().BeEmpty();

        var oversized = new PaginationRequest(1, new PageSize(1000, 1000), updateTotalCount: true);
        var oversizedUrl = collectionPath.AppendQueryParam(oversized.Serialize(SerializationMethod.QueryString));
        var bounded = await client.GetFromJsonAsync<PaginationResultModel<CategoryDto>>(oversizedUrl);
        bounded.Should().NotBeNull();
        bounded!.Items.Should().HaveCount(100);
        bounded.Info.Request.PageSize.Size.Should().Be(100);
        bounded.Info.Request.PageSize.MaxSize.Should().Be(100);
        bounded.Info.Total.Count.Should().BeGreaterThanOrEqualTo(202);

        var nextOversized = bounded.Info.Request.GetNextPage(bounded.Info.FirstId, bounded.Info.LastId);
        nextOversized = new PaginationRequest(nextOversized.PageNumber, new PageSize(1000, 1000), nextOversized.PageCursor);
        var cursorUrl = collectionPath.AppendQueryParam(nextOversized.Serialize(SerializationMethod.QueryString));
        var cursorPage = await client.GetFromJsonAsync<PaginationResultModel<CategoryDto>>(cursorUrl);
        cursorPage.Should().NotBeNull();
        cursorPage!.Items.Should().HaveCount(100);
        cursorPage.Info.Request.PageSize.MaxSize.Should().Be(100);
        cursorPage.Items.Select(item => item.Id).Intersect(bounded.Items.Select(item => item.Id)).Should().BeEmpty();

        var optionsPath = pathBase + "/" + Get.Endpoint.QA.Category.PaginateWithOptionsAsync.Path().TrimStart('/');
        var optionsPage = await client.GetFromJsonAsync<PaginationResultModel<CategoryDto>>(
            optionsPath.AppendQueryParam("pageSize", 1000).AppendQueryParam("maxSize", 1000));
        optionsPage.Should().NotBeNull();
        optionsPage!.Items.Should().HaveCount(100);
        var bodyPath = pathBase + "/" + Get.Endpoint.QA.Category.PaginateWithBodyAsync.Path().TrimStart('/');
        using var refreshResponse = await client.PostAsJsonAsync(bodyPath, first.Info.RequestRefresh());
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<PaginationResultModel<CategoryDto>>();
        refreshed.Should().NotBeNull();
        refreshed!.Items.Select(item => item.Id).Should().Equal(first.Items.Select(item => item.Id));
        refreshed.Info.HasNext.Should().BeTrue();
    }

    [Theory]
    [DataInline]
    public async Task Category_Should_Propagate_Cancellation_To_Database_Operations(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        using var scope = context.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
        var controller = new CategoryController(repository);
        var cancelled = new CancellationToken(true);

        Func<Task> list = () => controller.PaginateAsync(cancellationToken: cancelled);
        Func<Task> lookup = () => controller.GetAsync(Guid.NewGuid(), cancelled);
        Func<Task> create = () => controller.PostAsync(new CategoryPostRequest { Name = "Cancelled" }, cancelled);
        await list.Should().ThrowAsync<OperationCanceledException>();
        await lookup.Should().ThrowAsync<OperationCanceledException>();
        await create.Should().ThrowAsync<OperationCanceledException>();
    }
}
