namespace Sample.Hosted.Controllers.Sample;

[AllowAnonymous]
[ApiController]
[Route("Api/Sample/[controller]")]
public class ExceptionController
{
    [HttpGet(nameof(ValidationException))]
    public DrnException ValidationException()
        => throw new ValidationException("DrnTest");

    [HttpGet(nameof(UnauthorizedException))]
    public DrnException UnauthorizedException()
        => throw new UnauthorizedException("DrnTest");

    [HttpGet(nameof(ForbiddenException))]
    public DrnException ForbiddenException()
        => throw new ForbiddenException("DrnTest");

    [HttpGet(nameof(NotFoundException))]
    public DrnException NotFoundException()
        => throw new NotFoundException("DrnTest");

    [HttpGet(nameof(ConflictException))]
    public DrnException ConflictException()
        => throw new ConflictException("DrnTest");

    [HttpGet(nameof(ExpiredException))]
    public DrnException ExpiredException()
        => throw new ExpiredException("DrnTest");

    [HttpGet(nameof(ConfigurationException))]
    public DrnException ConfigurationException()
        => throw new ConfigurationException("DrnTest");

    [HttpGet(nameof(UnprocessableEntityException))]
    public DrnException UnprocessableEntityException()
        => throw new UnprocessableEntityException("DrnTest");

    [HttpGet(nameof(MaliciousRequestException))]
    public DrnException MaliciousRequestException()
        => throw new MaliciousRequestException("DrnTest");
}