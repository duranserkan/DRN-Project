using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Utils.Data.Serialization;
using DRN.Framework.Utils.Entity;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;
using DRN.Test.Integration.Tests.Sample.Pages.Helpers;
using Flurl;
using Microsoft.AspNetCore.Hosting;
using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Categories;
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

        var collectionPath = pathBase + Get.Endpoint.QA.Category.PaginateAsync.Path();
        using var unauthorized = await client.GetAsync(collectionPath);
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(client);

        using var created = await client.PostAsJsonAsync(pathBase + Get.Endpoint.QA.Category.PostAsync.Path(),
            new CategoryPostRequest { Name = "Contract category" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var expected = await created.Content.ReadFromJsonAsync<CategoryDto>();
        expected.Should().NotBeNull();
        expected!.Id.Should().NotBeEmpty();
        var location = created.Headers.Location;
        location.Should().NotBeNull();
        location!.ToString().Should().StartWith(pathBase + "/Api/QA/Category/");
        var actual = await client.GetFromJsonAsync<CategoryDto>(location);
        actual.Should().BeEquivalentTo(expected);

        using var missing = await client.GetAsync(pathBase + Get.Endpoint.QA.Category.GetAsync.Path(Guid.NewGuid()));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var problem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("type").GetString().Should().Be(CategoryProblemTypes.NotFound);
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        problem.RootElement.GetProperty("requestId").GetString().Should().NotBeNullOrWhiteSpace();
        problem.RootElement.TryGetProperty("stackTrace", out _).Should().BeFalse();

        // Leave more than a full page after the first bounded request to exercise the cursor ceiling too.
        using var scope = application.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<QAContext>();
        database.Categories.AddRange(Enumerable.Range(0, 201).Select(i => new Category($"Pagination category {i}")));
        await database.SaveChangesAsync();

        var first = await client.GetFromJsonAsync<PaginationResultModel<CategoryDto>>(collectionPath);
        first.Should().NotBeNull();
        first!.Items.Should().HaveCount(10);
        first.Info.Request.PageSize.MaxSize.Should().Be(100);
        first.Info.Request.UpdateTotalCount.Should().BeFalse();
        first.Info.Total.Count.Should().Be(-1);

        var minimum = await client.GetFromJsonAsync<PaginationResultModel<CategoryDto>>(
            collectionPath.AppendQueryParam("request.PageSize.Size", 0));
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
    public async Task Category_Should_Propagate_Cancellation_To_Direct_Database_Operations(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        using var scope = context.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<QAContext>();
        var pagination = scope.ServiceProvider.GetRequiredService<IPaginationUtils>();
        var controller = new CategoryController(database, pagination);
        var cancelled = new CancellationToken(true);

        Func<Task> list = () => controller.PaginateAsync(cancellationToken: cancelled);
        Func<Task> lookup = () => controller.GetAsync(Guid.NewGuid(), cancelled);
        Func<Task> create = () => controller.PostAsync(new CategoryPostRequest { Name = "Cancelled" }, cancelled);
        await list.Should().ThrowAsync<OperationCanceledException>();
        await lookup.Should().ThrowAsync<OperationCanceledException>();
        await create.Should().ThrowAsync<OperationCanceledException>();
    }
}
