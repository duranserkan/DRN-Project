namespace Sample.Hosted.Controllers.Sample;

[AllowAnonymous]
[ApiController]
[Route("Api/Sample/[controller]")]
public class ExceptionController
{
    [HttpGet(nameof(ValidationException))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public DrnException ValidationException()
        => throw new ValidationException("DrnTest");

    [HttpGet(nameof(UnauthorizedException))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public DrnException UnauthorizedException()
        => throw new UnauthorizedException("DrnTest");

    [HttpGet(nameof(ForbiddenException))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public DrnException ForbiddenException()
        => throw new ForbiddenException("DrnTest");

    [HttpGet(nameof(NotFoundException))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public DrnException NotFoundException()
        => throw new NotFoundException("DrnTest");

    [HttpGet(nameof(ConflictException))]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public DrnException ConflictException()
        => throw new ConflictException("DrnTest");

    [HttpGet(nameof(ExpiredException))]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public DrnException ExpiredException()
        => throw new ExpiredException("DrnTest");

    [HttpGet(nameof(ConfigurationException))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public DrnException ConfigurationException()
        => throw new ConfigurationException("DrnTest");

    [HttpGet(nameof(UnprocessableEntityException))]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public DrnException UnprocessableEntityException()
        => throw new UnprocessableEntityException("DrnTest");

    [HttpGet(nameof(MaliciousRequestException))]
    [ProducesResponseType(StatusCodes.Status406NotAcceptable)]
    public DrnException MaliciousRequestException()
        => throw new MaliciousRequestException("DrnTest");
}