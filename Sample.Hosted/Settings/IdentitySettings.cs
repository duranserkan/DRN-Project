using Microsoft.AspNetCore.Identity;

namespace Sample.Hosted.Settings;

public static class IdentitySettings
{
    public const bool RequireUniqueEmail = true;
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

    public static readonly LockoutOptions LockoutOptions = new LockoutOptions()
    {
        MaxFailedAccessAttempts = 3,
        AllowedForNewUsers = true,
        DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1)
    };

    public static readonly SignInOptions SignInOptions = new SignInOptions()
    {
        RequireConfirmedAccount = true,
        RequireConfirmedEmail = true,
        RequireConfirmedPhoneNumber = true
    };
}