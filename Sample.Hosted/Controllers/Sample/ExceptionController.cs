using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.Settings;

namespace Sample.Hosted.Controllers.Sample;

[ApiController]
[Route(ControllerRoutes.Sample)]
public class ExceptionController(IExceptionUtils exceptionUtils, IAppSettings appSettings) : ControllerBase
{
    private const string ExceptionMessage = "DrnTest";

    [AllowAnonymous]
    [HttpGet(nameof(ValidationException))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult ValidationException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new ValidationException(ExceptionMessage);
    }

    [AllowAnonymous]
    [HttpGet(nameof(UnauthorizedException))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult UnauthorizedException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new UnauthorizedException(ExceptionMessage);
    }

    [AllowAnonymous]
    [HttpGet(nameof(ForbiddenException))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult ForbiddenException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new ForbiddenException(ExceptionMessage);
    }

    [AllowAnonymous]
    [HttpGet(nameof(NotFoundException))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult NotFoundException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new NotFoundException(ExceptionMessage);
    }

    [AllowAnonymous]
    [HttpGet(nameof(ConflictException))]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult ConflictException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new ConflictException(ExceptionMessage);
    }

    [AllowAnonymous]
    [HttpGet(nameof(ExpiredException))]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult ExpiredException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new ExpiredException(ExceptionMessage);
    }

    [AllowAnonymous]
    [HttpGet(nameof(ConfigurationException))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult ConfigurationException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new ConfigurationException(ExceptionMessage);
    }

    [AllowAnonymous]
    [HttpGet(nameof(UnprocessableEntityException))]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult UnprocessableEntityException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new UnprocessableEntityException(ExceptionMessage);
    }

    [AllowAnonymous]
    [HttpGet(nameof(MaliciousRequestException))]
    [ProducesResponseType(StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult MaliciousRequestException()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        throw new MaliciousRequestException(ExceptionMessage);
    }

    [DeveloperExceptionPreview]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet(nameof(GetErrorPageModel))]
    [ProducesResponseType(typeof(DrnExceptionModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DrnExceptionModel>> GetErrorPageModel()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        try
        {
            throw new ConfigurationException(ExceptionMessage);
        }
        catch (Exception e)
        {
            var model = await exceptionUtils.CreateErrorPageModelAsync(HttpContext, e);

            return Ok(model);
        }
    }
}
