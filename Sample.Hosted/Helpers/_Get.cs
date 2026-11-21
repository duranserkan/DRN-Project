using DRN.Framework.Hosting.Endpoints;
using Sample.Domain.SampleIdentity.Users;
using Sample.Hosted.Pages.Shared.Models;
using Sample.Hosted.Settings;

namespace Sample.Hosted.Helpers;

public static class Get
{
    public static class Policy
    {
        public const string DeveloperException = DeveloperExceptionAccess.Policy;
        public const string SwaggerAccess = UserClaims.SwaggerAccess;
        public const string TestPagesAccess = UserClaims.TestPagesAccess;
        public const string QaApi = IdentitySettings.QaApiPolicy;
    }

    //PageFor and EndpointFor must be initialized first since they might be referenced from other helpers such as SubNavigationFor
    public static SamplePageFor Page { get; } = SamplePageFor.PageCollection;
    public static SampleEndpointFor Endpoint { get; } = SampleEndpointFor.EndpointCollection;

    public static CspFor Csp { get; } = new();
    public static RoleFor Role { get; } = new();
    public static ClaimFor Claim { get; } = new();
    public static RateLimitFor RateLimit { get; } = new();

    public static TempDataKeys TempDataKeys { get; } = new();
    public static ViewDataKeys ViewDataKeys { get; } = new();

    public static SubNavigationFor SubNavigation { get; } = new();
    public static LayoutOptionsFor LayoutOptions { get; } = new();
}
