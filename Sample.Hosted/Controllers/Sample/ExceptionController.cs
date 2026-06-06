using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.Settings;

namespace Sample.Hosted.Controllers.Sample;

[AllowAnonymous]
[ApiController]
[Route(SampleApiFor.ControllerRouteTemplate)]
public class ExceptionController(IExceptionUtils exceptionUtils, IAppSettings appSettings) : ControllerBase
{
    [HttpGet(nameof(ValidationException))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult ValidationException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new ValidationException("DrnTest");
    }

    [HttpGet(nameof(UnauthorizedException))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult UnauthorizedException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new UnauthorizedException("DrnTest");
    }

    [HttpGet(nameof(ForbiddenException))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult ForbiddenException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new ForbiddenException("DrnTest");
    }

    [HttpGet(nameof(NotFoundException))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult NotFoundException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new NotFoundException("DrnTest");
    }

    [HttpGet(nameof(ConflictException))]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult ConflictException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new ConflictException("DrnTest");
    }

    [HttpGet(nameof(ExpiredException))]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult ExpiredException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new ExpiredException("DrnTest");
    }

    [HttpGet(nameof(ConfigurationException))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult ConfigurationException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new ConfigurationException("DrnTest");
    }

    [HttpGet(nameof(UnprocessableEntityException))]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult UnprocessableEntityException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new UnprocessableEntityException("DrnTest");
    }

    [HttpGet(nameof(MaliciousRequestException))]
    [ProducesResponseType(StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult MaliciousRequestException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new MaliciousRequestException("DrnTest");
    }


    [HttpGet(nameof(GetErrorPageModel))]
    [ProducesResponseType(typeof(DrnExceptionModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DrnExceptionModel>> GetErrorPageModel()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        try
        {
            throw new ConfigurationException("DrnTest");
        }
        catch (Exception e)
        {
            var model = await exceptionUtils.CreateErrorPageModelAsync(HttpContext, e);

            return Ok(model);
        }
    }
}
