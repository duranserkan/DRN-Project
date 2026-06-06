using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Mvc;
using Sample.Hosted.Controllers.Sample;

namespace DRN.Test.Unit.Tests.Sample.Controller;

public class ExceptionControllerTests
{

    [Fact]
    public async Task Exception_Sample_Endpoints_Should_Be_Hidden_Outside_Development()
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(false);
        var controller = new ExceptionController(null!, appSettings);

        controller.ValidationException().Should().BeOfType<NotFoundResult>();
        controller.UnauthorizedException().Should().BeOfType<NotFoundResult>();
        controller.ForbiddenException().Should().BeOfType<NotFoundResult>();
        controller.NotFoundException().Should().BeOfType<NotFoundResult>();
        controller.ConflictException().Should().BeOfType<NotFoundResult>();
        controller.ExpiredException().Should().BeOfType<NotFoundResult>();
        controller.ConfigurationException().Should().BeOfType<NotFoundResult>();
        controller.UnprocessableEntityException().Should().BeOfType<NotFoundResult>();
        controller.MaliciousRequestException().Should().BeOfType<NotFoundResult>();

        var errorPageModel = await controller.GetErrorPageModel();
        errorPageModel.Result.Should().BeOfType<NotFoundResult>();
    }
}
