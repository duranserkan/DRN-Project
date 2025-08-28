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
        var postRequest4 = requests[3];

        result = await client.PostAsJsonAsync(postEndPoint, postRequest2);
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag2Expected = await result.Content.ReadFromJsonAsync<TagDto>();
        tag2Expected.Should().NotBeNull();
        tag2Expected.Model.Other.Should().Be(2);

        result = await client.PostAsJsonAsync(postEndPoint, postRequest3);
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag3Expected = await result.Content.ReadFromJsonAsync<TagDto>();
        tag3Expected.Should().NotBeNull();
        tag3Expected.Model.Other.Should().Be(3);

        result = await client.PostAsJsonAsync(postEndPoint, postRequest4);
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag4Expected = await result.Content.ReadFromJsonAsync<TagDto>();
        tag4Expected.Should().NotBeNull();
        tag4Expected.Model.Other.Should().Be(4);

        //Paginate
        var paginateEndpoint = Get.Endpoint.QA.Tag.PaginateAsync.Path()
            .AppendQueryParam(nameof(PaginationRequest.PageSize), 1)
            .AppendQueryParam(nameof(PaginationRequest.PageSize.MaxSize), 2)
            .AppendQueryParam(nameof(PaginationRequest.UpdateTotalCount), true);
        result = await client.GetAsync(paginateEndpoint);
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPageResult = await result.Content.ReadFromJsonAsync<PaginationResultModel<TagDto>>();
        firstPageResult.Should().NotBeNull();
        firstPageResult.Items.Count.Should().Be(1);
        firstPageResult.Info.Request.PageNumber.Should().Be(1);
        firstPageResult.Info.Request.PageSize.Size.Should().Be(1);
        firstPageResult.Info.Request.PageSize.MaxSize.Should().Be(2);
        firstPageResult.Info.Request.UpdateTotalCount.Should().BeTrue();
        firstPageResult.Info.TotalCountUpdated.Should().BeTrue();
        firstPageResult.Info.Total.Count.Should().BeGreaterThanOrEqualTo(3);
        firstPageResult.Info.Total.Pages.Should().BeGreaterThanOrEqualTo(3);
        firstPageResult.Info.HasNext.Should().BeTrue();
        firstPageResult.Info.HasPrevious.Should().BeFalse();
        firstPageResult.Info.Request.PageCursor.Should().BeEquivalentTo(PageCursor.Initial);
        var firstItem = firstPageResult.Items.First();

        //Paginate Page 2 with Get
        var queryStringPage2 = QueryParameterSerializer.SerializeToQueryString(firstPageResult.Info);
        var paginateWithQueryEndpoint = Get.Endpoint.QA.Tag.PaginateWithQueryAsync.Path()
            .AppendQueryParam(queryStringPage2)
            .AppendQueryParam("jumpTo", 2)!;

        result = await client.GetAsync(paginateWithQueryEndpoint);
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPageResult = await result.Content.ReadFromJsonAsync<PaginationResultModel<TagDto>>();
        secondPageResult.Should().NotBeNull();
        secondPageResult.Items.Count.Should().Be(1);
        secondPageResult.Info.Request.PageNumber.Should().Be(2);
        secondPageResult.Info.Request.PageSize.Size.Should().Be(1);
        secondPageResult.Info.Request.PageSize.MaxSize.Should().Be(2);
        secondPageResult.Info.Request.UpdateTotalCount.Should().Be(false);
        secondPageResult.Info.HasNext.Should().BeTrue();
        secondPageResult.Info.HasPrevious.Should().BeTrue();
        secondPageResult.Info.Request.PageCursor.PageNumber.Should().Be(1);
        secondPageResult.Info.Request.PageCursor.SortDirection.Should().Be(PageSortDirection.Ascending);
        secondPageResult.Info.Request.PageCursor.FirstId.Should().Be(firstItem.Id);
        secondPageResult.Info.Request.PageCursor.LastId.Should().Be(firstItem.Id);

        //Paginate Page 3 with Post
        var paginateWithBodyEndpoint = Get.Endpoint.QA.Tag.PaginateWithBodyAsync.Path()
            .AppendQueryParam("jumpTo", 3)
            .AppendQueryParam(nameof(PaginationRequest.UpdateTotalCount), true);
        result = await client.PostAsJsonAsync(paginateWithBodyEndpoint, firstPageResult.Info);
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        var thirdPageResultPost = await result.Content.ReadFromJsonAsync<PaginationResultModel<TagDto>>();
        thirdPageResultPost.Should().NotBeNull();
        thirdPageResultPost.Items.Count.Should().Be(1);
        thirdPageResultPost.Info.Request.PageNumber.Should().Be(3);
        thirdPageResultPost.Info.Request.PageSize.Size.Should().Be(1);
        thirdPageResultPost.Info.Request.PageSize.MaxSize.Should().Be(2);
        thirdPageResultPost.Info.Request.UpdateTotalCount.Should().Be(true);
        thirdPageResultPost.Info.HasPrevious.Should().BeTrue();
        //The test database is shared. Other integration tests can insert tags, so evaluating HasNext is not right.

        //Return Page 2 with Get
        var queryStringPage2Return = QueryParameterSerializer.SerializeToQueryString(thirdPageResultPost.Info);
        var paginateWithQueryEndpointPage2Return = Get.Endpoint.QA.Tag.PaginateWithQueryAsync.Path()
            .AppendQueryParam(queryStringPage2Return)
            .AppendQueryParam("jumpTo", 2)
            .AppendQueryParam(nameof(PaginationRequest.UpdateTotalCount), false);
        result = await client.GetAsync(paginateWithQueryEndpointPage2Return);
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPageResultReturnGet = await result.Content.ReadFromJsonAsync<PaginationResultModel<TagDto>>();
        secondPageResultReturnGet.Should().NotBeNull();
        secondPageResultReturnGet.Items.Count.Should().Be(1);
        secondPageResultReturnGet.Info.Request.PageNumber.Should().Be(2);
        secondPageResultReturnGet.Info.Request.PageSize.Size.Should().Be(1);
        secondPageResultReturnGet.Info.Request.PageSize.MaxSize.Should().Be(2);
        secondPageResultReturnGet.Info.Request.UpdateTotalCount.Should().Be(false);
        secondPageResultReturnGet.Info.HasNext.Should().BeTrue();
        secondPageResultReturnGet.Info.HasPrevious.Should().BeTrue();

        //Paginate Page 3 with Get
        var queryStringPage3 = QueryParameterSerializer.SerializeToQueryString(secondPageResult.Info);
        paginateWithQueryEndpoint = Get.Endpoint.QA.Tag.PaginateWithQueryAsync.Path()
            .AppendQueryParam(queryStringPage3)
            .AppendQueryParam("jumpTo", 3)
            .AppendQueryParam(nameof(PaginationRequest.UpdateTotalCount), true);

        result = await client.GetAsync(paginateWithQueryEndpoint);
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        var thirdPageResultGet = await result.Content.ReadFromJsonAsync<PaginationResultModel<TagDto>>();
        thirdPageResultGet.Should().NotBeNull();
        thirdPageResultGet.Items.Count.Should().Be(1);
        thirdPageResultGet.Info.Request.PageNumber.Should().Be(3);
        thirdPageResultGet.Info.Request.PageSize.Size.Should().Be(1);
        thirdPageResultGet.Info.Request.PageSize.MaxSize.Should().Be(2);
        thirdPageResultGet.Info.Request.UpdateTotalCount.Should().Be(true);
        thirdPageResultGet.Info.HasPrevious.Should().BeTrue();
        //The test database is shared. Other integration tests can insert tags, so evaluating HasNext is not right.

        //Return Page 2 with Post
        var paginateWithPostEndpointPage2Return = Get.Endpoint.QA.Tag.PaginateWithBodyAsync.Path()
            .AppendQueryParam("jumpTo", 2)
            .AppendQueryParam(nameof(PaginationRequest.UpdateTotalCount), false);
        result = await client.PostAsJsonAsync(paginateWithPostEndpointPage2Return, thirdPageResultGet.Info);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        secondPageResultReturnGet.Should().NotBeNull();
        secondPageResultReturnGet.Items.Count.Should().Be(1);
        secondPageResultReturnGet.Info.Request.PageNumber.Should().Be(2);
        secondPageResultReturnGet.Info.Request.PageSize.Size.Should().Be(1);
        secondPageResultReturnGet.Info.Request.PageSize.MaxSize.Should().Be(2);
        secondPageResultReturnGet.Info.Request.UpdateTotalCount.Should().Be(false);
        secondPageResultReturnGet.Info.HasNext.Should().BeTrue();
        secondPageResultReturnGet.Info.HasPrevious.Should().BeTrue();
    }

    private TagPostRequest[] GetTagPostRequests()
    {
        var postRequest1 = new TagPostRequest
        {
            Name = nameof(TagController_Should_Return_Tags) + "1"
        };

        postRequest1.Model.StringValue = "Liberté";
        postRequest1.Model.Other = 1;
        postRequest1.Model.Type = TagType.User;

        var postRequest2 = new TagPostRequest
        {
            Name = nameof(TagController_Should_Return_Tags) + "2"
        };
        postRequest2.Model.StringValue = "Égalité";
        postRequest2.Model.Other = 2;
        postRequest2.Model.Type = TagType.User;

        var postRequest3 = new TagPostRequest
        {
            Name = nameof(TagController_Should_Return_Tags) + "3"
        };
        postRequest3.Model.StringValue = "Fraternité";
        postRequest3.Model.Other = 3;
        postRequest3.Model.Type = TagType.User;

        var postRequest4 = new TagPostRequest
        {
            Name = nameof(TagController_Should_Return_Tags) + "4"
        };
        postRequest4.Model.StringValue = "Vive la République";
        postRequest4.Model.Other = 4;
        postRequest4.Model.Type = TagType.User;


        return [postRequest1, postRequest2, postRequest3, postRequest4];
    }
}