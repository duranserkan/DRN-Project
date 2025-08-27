using System.Net;
using System.Net.Http.Json;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Utils.Encodings;
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
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPageResult = await result.Content.ReadFromJsonAsync<PaginationResultModel<TagDto>>();
        firstPageResult.Should().NotBeNull();
        firstPageResult.Items.Count.Should().Be(1);
        firstPageResult.Info.Request.PageNumber.Should().Be(1);

        //Paginate Second with Post
        var paginateWithBodyEndpoint = Get.Endpoint.QA.Tag.PaginateWithBodyAsync.Path().AppendQueryParam("jumpTo", "2")!;
        result = await client.PostAsJsonAsync(paginateWithBodyEndpoint, firstPageResult.Info);
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPageResult = await result.Content.ReadFromJsonAsync<PaginationResultModel<TagDto>>();
        secondPageResult.Should().NotBeNull();
        secondPageResult.Items.Count.Should().Be(1);
        secondPageResult.Info.Request.PageNumber.Should().Be(2);


        //Paginate Second with Get
        var qs = QueryParameterSerializer.SerializeToQueryString(secondPageResult.Info);

        var paginateWithQueryEndpoint = Get.Endpoint.QA.Tag.PaginateWithQueryAsync.Path()
            .AppendQueryParam(qs)
            .AppendQueryParam("jumpTo", "2")!;
        paginateWithQueryEndpoint.SetQueryParams(secondPageResult.Info.Request);

        result = await client.GetAsync(paginateWithQueryEndpoint);
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        secondPageResult = await result.Content.ReadFromJsonAsync<PaginationResultModel<TagDto>>();
        secondPageResult.Should().NotBeNull();
        secondPageResult.Items.Count.Should().Be(1);
        secondPageResult.Info.Request.PageNumber.Should().Be(2);
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
        postRequest2.Model.StringValue = "Égalité";
        postRequest2.Model.Other = 2;
        postRequest2.Model.Type = TagType.User;

        var postRequest3 = new TagPostRequest
        {
            Name = nameof(TagController_Should_Return_Tags)
        };
        postRequest3.Model.StringValue = "Fraternité";
        postRequest3.Model.Other = 3;
        postRequest3.Model.Type = TagType.User;
        
        return [postRequest1, postRequest2, postRequest3];
    }
}