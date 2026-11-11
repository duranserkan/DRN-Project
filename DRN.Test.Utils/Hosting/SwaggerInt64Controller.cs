using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Test.Utils.Hosting;

[ApiController]
[Route("swagger-int64")]
public sealed class SwaggerInt64Controller : ControllerBase
{
    [HttpPost("nullable")]
    public JsonResult Nullable([FromBody] long? value) => new(value);

    [HttpPost("required")]
    public JsonResult Required([FromBody] long value) => new(value);

    [HttpPost("required-nullable")]
    public JsonResult RequiredNullable([FromBody, Required] long? value) => new(value);
}
