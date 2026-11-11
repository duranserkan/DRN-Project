using DRN.Framework.SharedKernel.Domain.Pagination;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Test.Utils.Hosting;

[ApiController]
[Route("swagger-pagination")]
public sealed class SwaggerPaginationController : ControllerBase
{
    [HttpGet("query-request")]
    public IActionResult QueryRequest([FromQuery] PaginationRequest request) => Ok();

    [HttpPost("post-query-request")]
    public IActionResult PostQueryRequest([FromQuery] PaginationRequest request) => Ok();

    [HttpGet("query-info")]
    public IActionResult QueryInfo([FromQuery(Name = "previous")] PaginationResultInfo info) => Ok();

    [HttpPost("body-info")]
    [EndpointDescription("Application continuation policy.")]
    public IActionResult BodyInfo([FromBody] PaginationResultInfo info,
        [FromQuery(Name = "destination")] int target = 1) => Ok();

    [HttpPost("body-request")]
    public IActionResult BodyRequest([FromBody] PaginationRequest request) => Ok();

    [HttpGet("result")]
    public PaginationResultModel<string> Result() => new(
        new PaginationResultInfo(PaginationRequest.Default, Guid.Empty, Guid.Empty, 0, false, false, PaginationTotal.NotSpecified), []);

    [HttpGet("unrelated")]
    [EndpointDescription("Unrelated operation.")]
    public string Unrelated([FromQuery] int jumpTo = 1) => "unrelated";
}
