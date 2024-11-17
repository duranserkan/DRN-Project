using Microsoft.AspNetCore.Identity;

namespace Sample.Hosted.Settings;


//https://learn.microsoft.com/en-us/aspnet/core/security/?view=aspnetcore-8.0
//https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity
//https://learn.microsoft.com/en-us/aspnet/core/security/authentication/mfa?view=aspnetcore-8.0
//https://learn.microsoft.com/en-us/aspnet/core/security/authorization/limitingidentitybyscheme?view=aspnetcore-8.0
//https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/microsoft-logins?view=aspnetcore-8.0
//https://stackoverflow.com/questions/52492666/what-is-the-point-of-configuring-defaultscheme-and-defaultchallengescheme-on-asp
//IdentityConstants.BearerAndApplicationScheme; //ClaimsIdentity.DefaultIssuer; //ClaimTypes.Name;
public static class IdentitySettings
{
    public const string AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    public const int MinPasswordLength = 8;


    public static readonly UserOptions UserOptions = new()
    {
        RequireUniqueEmail = true,
        AllowedUserNameCharacters = AllowedUserNameCharacters
    };

    public static readonly PasswordOptions PasswordOptions = new()
    {
        RequireDigit = true,
        RequireUppercase = true,
        RequireLowercase = true,
        RequiredLength = MinPasswordLength,
        RequiredUniqueChars = 1,
        RequireNonAlphanumeric = true
    };

    public static readonly LockoutOptions LockoutOptions = new()
    {
        MaxFailedAccessAttempts = 3,
        AllowedForNewUsers = true,
        DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1)
    };

    public static readonly SignInOptions SignInOptions = new()
    {
        RequireConfirmedAccount = true,
        RequireConfirmedEmail = true,
        RequireConfirmedPhoneNumber = true
    };

    public static readonly ClaimsIdentityOptions ClaimsIdentityOptions = new();
    public static readonly StoreOptions StoreOptions = new();
    public static readonly TokenOptions TokenOptions = new();
}