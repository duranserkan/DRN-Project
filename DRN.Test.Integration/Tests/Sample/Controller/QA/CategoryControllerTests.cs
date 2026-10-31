using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Utils.Data.Serialization;
using DRN.Framework.Utils.Entity;
using DRN.Framework.Utils.Ids;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;
using DRN.Test.Integration.Tests.Sample.Pages.Helpers;
using Flurl;
using Microsoft.AspNetCore.Hosting;
using Sample.Application.Services;
using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Categories;
using Sample.Domain.QA.Tags;
using Sample.Hosted;
using Sample.Hosted.Controllers.QA;
using Sample.Hosted.Helpers;
using Sample.Infra.QA;

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

        var collectionPath = pathBase + "/" + Get.Endpoint.QA.Category.PaginateAsync.Path().TrimStart('/');
        collectionPath.Should().Be(pathBase + "/Api/QA/Category");
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
        var location = created.Headers.Location;
        location.Should().NotBeNull();
        location!.ToString().Should().StartWith(pathBase + "/Api/QA/Category/");
        var actual = await client.GetFromJsonAsync<CategoryDto>(location);
        actual.Should().BeEquivalentTo(expected);

        using var scope = application.Services.CreateScope();
        var entityIds = scope.ServiceProvider.GetRequiredService<ISourceKnownEntityIdUtils>();
        var categoryId = entityIds.Parse(expected.Id);
        var wrongTypeId = entityIds.Generate<Tag>(categoryId.Source.Id).EntityId;
        var absentId = entityIds.Generate<Category>().EntityId;
        foreach (var missingId in new[] { Guid.NewGuid(), absentId, wrongTypeId })
        {
            var missingPath = pathBase + "/" + Get.Endpoint.QA.Category.GetAsync.Path(missingId).TrimStart('/');
            using var missing = await client.GetAsync(missingPath);
            missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
            missing.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            using var problem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
            problem.RootElement.GetProperty("type").GetString().Should().Be(
                Microsoft.AspNetCore.Http.TypedResults.Problem(statusCode: 404).ProblemDetails.Type);
            problem.RootElement.GetProperty("title").GetString().Should().Be("Category not found");
            problem.RootElement.GetProperty("status").GetInt32().Should().Be(404);
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
    }

    [Theory]
    [DataInline]
    public async Task Category_Should_Propagate_Cancellation_To_Database_Operations(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        using var scope = context.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<QAContext>();
        var pagination = scope.ServiceProvider.GetRequiredService<IPaginationUtils>();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var controller = new CategoryController(database, pagination, categories);
        var cancelled = new CancellationToken(true);

        Func<Task> list = () => controller.PaginateAsync(cancellationToken: cancelled);
        Func<Task> lookup = () => controller.GetAsync(Guid.NewGuid(), cancelled);
        Func<Task> create = () => controller.PostAsync(new CategoryPostRequest { Name = "Cancelled" }, cancelled);
        await list.Should().ThrowAsync<OperationCanceledException>();
        await lookup.Should().ThrowAsync<OperationCanceledException>();
        await create.Should().ThrowAsync<OperationCanceledException>();
    }
}
