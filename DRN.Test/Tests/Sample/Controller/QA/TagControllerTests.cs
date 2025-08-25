using System.Net;
using System.Net.Http.Json;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Test.Tests.Sample.Controller.Helpers;
using Flurl;
using Sample.Contract.QA.Tags;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Tests.Sample.Controller.QA;

public class TagControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task TagController_Should_Return_Tags(TestContext context)
    {
        var getEndpoint = Get.Endpoint.QA.Tag.GetAsync.Path(Guid.NewGuid());
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        var requests = GetTagPostRequests();

        //Unauthorized 
        var result = await client.GetAsync(getEndpoint);
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        //Bad Request 
        var user = await AuthenticationHelper<SampleProgram>.AuthenticateClientAsync(client);
        result = await client.GetAsync(getEndpoint);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        //Create 
        var postRequest1 = requests[0];
        var postEndPoint = Get.Endpoint.QA.Tag.PostAsync.Path();
        result = await client.PostAsJsonAsync(postEndPoint, postRequest1);
        result.StatusCode.Should().Be(HttpStatusCode.Created);

        var tag1Expected = await result.Content.ReadFromJsonAsync<TagDto>();
        tag1Expected.Should().NotBeNull();
        tag1Expected.Model.Should().BeEquivalentTo(postRequest1.Model);
        tag1Expected.Name.Should().Be(postRequest1.Name);
        tag1Expected.Id.Should().NotBeEmpty();

        getEndpoint = result.Headers.Location?.ToString();
        getEndpoint.Should().NotBeNull();
        result = await client.GetAsync(getEndpoint);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var tag1Actual = await result.Content.ReadFromJsonAsync<TagDto>();
        tag1Actual.Should().BeEquivalentTo(tag1Expected);

        //Delete 
        var deleteEndPoint = Get.Endpoint.QA.Tag.DeleteAsync.Path(tag1Actual.Id);
        result = await client.DeleteAsync(deleteEndPoint);
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);

        //Not Found
        result = await client.GetAsync(getEndpoint);
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);

        //Seed
        var postRequest2 = requests[1];
        var postRequest3 = requests[2];

        result = await client.PostAsJsonAsync(postEndPoint, postRequest2);
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag2Expected = await result.Content.ReadFromJsonAsync<TagDto>();

        result = await client.PostAsJsonAsync(postEndPoint, postRequest3);
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag3Expected = await result.Content.ReadFromJsonAsync<TagDto>();

        //Paginate
        var paginateEndpoint = Get.Endpoint.QA.Tag.PaginateAsync.Path().AppendQueryParam("pageSize", "1");
        result = await client.GetAsync(paginateEndpoint);
        var content = await result.Content.ReadAsStringAsync();

        // var tags = await result.Content.ReadFromJsonAsync<PaginationResultModel<TagDto>>();
        // tags.Should().NotBeNull();
        //
        // tags.Items.Count.Should().Be(1);
        // tags.Items[0].Should().BeEquivalentTo(tag2Expected);
    }

    //todo use data generator
    private TagPostRequest[] GetTagPostRequests()
    {
        var postRequest1 = new TagPostRequest
        {
            Name = nameof(TagController_Should_Return_Tags)
        };

        postRequest1.Model.StringValue = "Liberté";
        postRequest1.Model.Other = 1;
        postRequest1.Model.Type = TagType.User;

        var postRequest2 = new TagPostRequest
        {
            Name = nameof(TagController_Should_Return_Tags)
        };
        postRequest1.Model.StringValue = "Égalité";
        postRequest1.Model.Other = 2;
        postRequest1.Model.Type = TagType.User;

        var postRequest3 = new TagPostRequest
        {
            Name = nameof(TagController_Should_Return_Tags)
        };
        postRequest1.Model.StringValue = "Fraternité";
        postRequest1.Model.Other = 3;
        postRequest1.Model.Type = TagType.User;


        return [postRequest1, postRequest2, postRequest3];
    }
}

public static class TemplateExtensions
{
    public static string GetPath(this ApiEndpoint endpoint, Guid id)
        => endpoint.RoutePattern!.Replace("{id:guid}", id.ToString("N"));
}