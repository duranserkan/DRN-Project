using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Utils.Data.Serialization;
using DRN.Test.Integration.Tests.Sample.Controller.Helpers;
using Flurl;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Sample.Contract.QA.Tags;
using Sample.Hosted.Controllers.QA;

namespace DRN.Test.Integration.Tests.Sample.Controller.QA;

public class TagControllerTests
{
    [Theory]
    [DataInline]
    public async Task TagController_Should_Use_Standard_Crud_And_Pagination_Contracts(DrnTestContext context)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>();
        var endpoints = Get.Endpoint.QA.Tag;
        using var anonymous = await client.GetAsync(endpoints.GetAsync.Path(Guid.NewGuid()));
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(client);

        using var invalid = await client.GetAsync(endpoints.GetAsync.Path(Guid.NewGuid()));
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        foreach (var invalidPayload in new[]
                 {
                     """{"name":"Omitted type","model":{"stringValue":"abc"}}""",
                     """{"name":"System tag","model":{"type":"System"}}""",
                     """{"name":"System zero","model":{"type":0}}""",
                     """{"name":"Undefined type","model":{"type":99}}""",
                     """{"name":"Unexpected field","model":{"type":"User","isApproved":true}}"""
                 })
        {
            using var content = new StringContent(invalidPayload, System.Text.Encoding.UTF8, "application/json");
            using var rejected = await client.PostAsync(endpoints.PostAsync.Path(), content);
            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        var createdTags = new List<TagResponse>();
        for (var index = 0; index < 4; index++)
        {
            var request = new TagPostRequest
            {
                Name = $"Standard tag {index}",
                Model = new TagModelPostRequest
                {
                    Other = index,
                    StringValue = "Liberté",
                    Type = TagType.User
                }
            };
            using var created = await client.PostAsJsonAsync(endpoints.PostAsync.Path(), request);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            created.Headers.Location.Should().BeNull();
            var response = await created.Content.ReadFromJsonAsync<TagResponse>();
            response.Should().NotBeNull();
            response.Id.Should().NotBeEmpty();
            response.Name.Should().Be(request.Name);
            response.Model.Should().BeEquivalentTo(request.Model);
            createdTags.Add(response);
            var fetched = await client.GetFromJsonAsync<TagResponse>(endpoints.GetAsync.Path(response.Id));
            fetched.Should().BeEquivalentTo(response);
        }

        using var deleted = await client.DeleteAsync(endpoints.DeleteAsync.Path(createdTags[0].Id));
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var missing = await client.GetAsync(endpoints.GetAsync.Path(createdTags[0].Id));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var optionsUrl = endpoints.PaginateWithOptionsAsync.Path()
            .AppendQueryParam("pageSize", 1).AppendQueryParam("maxSize", 1000)
            .AppendQueryParam("totalCount", 1000000);
        var options = await client.GetFromJsonAsync<PaginationResultModel<TagResponse>>(optionsUrl);
        options.Should().NotBeNull();
        options.Items.Should().HaveCount(1);
        options.Info.Request.PageSize.MaxSize.Should().Be(100);
        options.Info.Total.Count.Should().Be(-1);

        var firstRequest = PaginationRequest.DefaultWith(size: 1, updateTotalCount: true);
        var firstUrl = endpoints.PaginateAsync.Path().AppendQueryParam(firstRequest.Serialize(SerializationMethod.QueryString));
        var first = await client.GetFromJsonAsync<PaginationResultModel<TagResponse>>(firstUrl);
        first.Should().NotBeNull();
        first.Items.Should().HaveCount(1);
        first.Info.Request.PageNumber.Should().Be(1);
        first.Info.Total.Count.Should().BeGreaterThanOrEqualTo(3);
        first.Info.HasNext.Should().BeTrue();
        first.Info.HasPrevious.Should().BeFalse();

        var nextUrl = endpoints.PaginateAsync.Path()
            .AppendQueryParam(first.Info.RequestNextPage().Serialize(SerializationMethod.QueryString));
        var second = await client.GetFromJsonAsync<PaginationResultModel<TagResponse>>(nextUrl);
        second.Should().NotBeNull();
        second.Info.Request.PageNumber.Should().Be(2);
        second.Items.Should().HaveCount(1);
        second.Items[0].Id.Should().NotBe(first.Items[0].Id);
        second.Info.Total.Count.Should().Be(-1);

        using var thirdResponse = await client.PostAsJsonAsync(endpoints.PaginateWithBodyAsync.Path(),
            second.Info.RequestNextPage(updateTotalCount: true));
        thirdResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var third = await thirdResponse.Content.ReadFromJsonAsync<PaginationResultModel<TagResponse>>();
        third.Should().NotBeNull();
        third.Info.Request.PageNumber.Should().Be(3);
        third.Info.TotalCountUpdated.Should().BeTrue();
        third.Items.Should().HaveCount(1);

        using var previousResponse = await client.PostAsJsonAsync(endpoints.PaginateWithBodyAsync.Path(), third.Info.RequestPreviousPage());
        previousResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var previous = await previousResponse.Content.ReadFromJsonAsync<PaginationResultModel<TagResponse>>();
        previous.Should().NotBeNull();
        previous.Info.Request.PageNumber.Should().Be(2);
        previous.Items.Select(item => item.Id).Should().Equal(second.Items.Select(item => item.Id));
        previous.Info.Request.PageSize.MaxSize.Should().Be(100);
        previous.Info.Total.Count.Should().Be(-1);

        foreach (var path in new[] { "Any", "Count", "PaginateWithQuery", "PaginateWithOptions" })
        {
            using var disabled = await client.GetAsync($"/Api/QA/Tag/{path}");
            disabled.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        using var lookup = await client.PostAsJsonAsync("/Api/QA/Tag/Lookup", new[] { createdTags[1].Id });
        lookup.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        // HTTP method rejection does not prove that MVC removed the action.
        using var scope = context.CreateScope();
        var actions = scope.ServiceProvider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(TagController));
        actions.Should().NotContain(action => action.MethodInfo.Name == nameof(TagController.GetManyAsync));
    }
}
