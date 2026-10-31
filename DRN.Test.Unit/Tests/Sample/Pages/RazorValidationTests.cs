using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sample.Application.Services;
using Sample.Domain.Users;
using Sample.Hosted.Extensions;
using Sample.Hosted.Pages.User.Management;
using Sample.Hosted.Pages.User.Profile;

namespace DRN.Test.Unit.Tests.Sample.Pages;

public class RazorValidationTests
{
    [Fact]
    public async Task Oversized_Picture_Should_Report_The_Bound_Field_Without_Saving()
    {
        var service = Substitute.For<IProfilePictureService>();
        var upload = Substitute.For<IFormFile>();
        upload.Length.Returns(100 * 1024 + 1);
        var page = new ProfilePictureModel(service, null!, null!)
        {
            Input = new ProfilePictureInput { ProfilePicture = upload },
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

        (await page.OnPostAsync()).Should().BeOfType<PageResult>();

        page.ModelState.Keys.Should().Equal("Input.ProfilePicture");
        page.ModelState["Input.ProfilePicture"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("The uploaded file is too large. Maximum allowed size is 100KB.");
        await service.DidNotReceive().CreateProfilePictureAsync(Arg.Any<SampleUser>(), Arg.Any<Stream>(), Arg.Any<long>());
    }

    [Fact]
    public async Task Rejected_Picture_Should_Show_Safe_Field_Guidance_Without_Refreshing_SignIn()
    {
        using var users = CreateUserManager();
        var signIn = CreateSignInManager(users);
        var user = new SampleUser { Id = "user" };
        users.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        var service = Substitute.For<IProfilePictureService>();
        service.CreateProfilePictureAsync(user, Arg.Any<Stream>(), 100 * 1024)
            .Returns(Task.FromException(ExceptionFor.Validation("private-storage-diagnostic")));
        using var content = new MemoryStream([0x01]);
        var page = new ProfilePictureModel(service, users, signIn)
        {
            Input = new ProfilePictureInput { ProfilePicture = new FormFile(content, 0, content.Length, "Input.ProfilePicture", "image.jpg") },
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

        (await page.OnPostAsync()).Should().BeOfType<PageResult>();

        page.ModelState.Keys.Should().Equal("Input.ProfilePicture");
        page.ModelState["Input.ProfilePicture"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Please upload a valid JPEG image no larger than 100KB.")
            .And.NotContain("private-storage-diagnostic");
        _ = service.Received(1).CreateProfilePictureAsync(user, Arg.Any<Stream>(), 100 * 1024);
        await signIn.DidNotReceive().RefreshSignInAsync(Arg.Any<SampleUser>());
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Invalid_Razor_ModelState_Should_Remain_A_Page_Result_Even_For_Htmx(bool htmx)
    {
        var http = new DefaultHttpContext();
        if (htmx) http.Request.Headers["HX-Request"] = "true";
        var page = new ProfilePictureModel(null!, null!, null!)
        {
            Input = new ProfilePictureInput { ProfilePicture = Substitute.For<IFormFile>() },
            PageContext = new PageContext { HttpContext = http }
        };
        page.ModelState.AddModelError("Input.ProfilePicture", "Choose a picture.");

        (await page.OnPostAsync()).Should().BeOfType<PageResult>();

        page.ModelState["Input.ProfilePicture"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Choose a picture.");
        http.Response.StatusCode.Should().Be(200);
        http.Response.ContentType.Should().BeNull();
    }

    [Fact]
    public void Registration_Errors_Should_Keep_Descriptions_At_Form_Level()
    {
        var page = new ValidationPageModel { PageContext = new PageContext { HttpContext = new DefaultHttpContext() } };
        var result = IdentityResult.Failed(
            new IdentityError { Code = "DuplicateEmail", Description = "This email is already registered." },
            new IdentityError { Code = "PasswordTooShort", Description = "Choose a longer password." });

        page.ReturnPageWithUserRegisterErrors(result).Should().BeOfType<PageResult>();

        page.ModelState.Keys.Should().Equal(string.Empty);
        page.ModelState[string.Empty]!.Errors.Select(error => error.ErrorMessage).Should().Equal(
            "This email is already registered.", "Choose a longer password.");
    }

    [Theory]
    [DataInlineUnit]
    public async Task Rejected_Authenticator_Code_Should_Target_The_Field_Without_Changing_Mfa(DrnTestContextUnit services)
    {
        var claims = AuthenticationClaimConfig.Default;
        services.ServiceCollection.AddSingleton(claims);
        services.ServiceCollection.AddSingleton(Options.Create(new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build()
        }));
        ScopeContext.InitializeForTest(services);
        using var users = CreateUserManager();
        var signIn = CreateSignInManager(users);
        var user = new SampleUser { Id = "user", Email = "user@example.test" };
        users.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        users.GetTwoFactorEnabledAsync(user).Returns(false);
        users.GetAuthenticatorKeyAsync(user).Returns("abcdefghijklmnop");
        users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, "123456").Returns(false);
        var http = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(claims.Subject.Type, user.Id),
                new Claim(ClaimTypes.AuthenticationMethod, MfaClaimValues.MfaSetupRequired)
            ], IdentityConstants.ApplicationScheme))
        };
        var page = new EnableAuthenticator(signIn, users)
        {
            QrCodeVerify = new QrCodeVerifyModel { Code = "123-456" },
            PageContext = new PageContext { HttpContext = http }
        };

        (await page.OnPostVerifyAsync()).Should().BeOfType<PageResult>();

        page.ModelState.Keys.Should().Equal("QrCodeVerify.Code");
        page.ModelState["QrCodeVerify.Code"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Verification code is invalid.");
        await users.Received(1).VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, "123456");
        await users.DidNotReceive().SetTwoFactorEnabledAsync(Arg.Any<SampleUser>(), Arg.Any<bool>());
        await users.DidNotReceive().GenerateNewTwoFactorRecoveryCodesAsync(Arg.Any<SampleUser>(), Arg.Any<int>());
        await signIn.DidNotReceive().SignOutAsync();
    }

    private static UserManager<SampleUser> CreateUserManager() => Substitute.For<UserManager<SampleUser>>(
        Substitute.For<IUserStore<SampleUser>>(), Options.Create(new IdentityOptions()), new PasswordHasher<SampleUser>(),
        Array.Empty<IUserValidator<SampleUser>>(), Array.Empty<IPasswordValidator<SampleUser>>(),
        new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), Substitute.For<IServiceProvider>(),
        NullLogger<UserManager<SampleUser>>.Instance);

    private static SignInManager<SampleUser> CreateSignInManager(UserManager<SampleUser> users) => Substitute.For<SignInManager<SampleUser>>(
        users, Substitute.For<IHttpContextAccessor>(), Substitute.For<IUserClaimsPrincipalFactory<SampleUser>>(),
        Options.Create(new IdentityOptions()), NullLogger<SignInManager<SampleUser>>.Instance,
        Substitute.For<IAuthenticationSchemeProvider>(), Substitute.For<IUserConfirmation<SampleUser>>());

    private sealed class ValidationPageModel : PageModel;
}
