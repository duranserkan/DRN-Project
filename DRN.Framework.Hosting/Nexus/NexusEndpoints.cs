namespace DRN.Framework.Hosting.Nexus;

public static class NexusEndpoints
{
    public const string Prefix = "Api";
    public const string StatusController = "Status";
    public const string WeatherForecastController = "WeatherForecast";
    public const string UserController = "User";

    public const string Status = $"{Prefix}/{StatusController}";
    public const string WeatherForecast = $"{Prefix}/{WeatherForecastController}";
    public const string User = $"{Prefix}/{UserController}";

    public static class Identity
    {
        public const string Prefix = $"{User}";

        public const string LoginController = "NexusIdentityLogin";
        public const string RegisterController = "NexusIdentityRegister";
        public const string PasswordController = "NexusIdentityPassword";
        public const string ManagementController = "NexusIdentityManagement";

        public const string Login = $"{Prefix}/{LoginController}/Login";
        public const string Refresh = $"{Prefix}/{LoginController}/Refresh";
        public const string Register = $"{Prefix}/{RegisterController}/Register";
        public const string ConfirmEmail = $"{Prefix}/{RegisterController}/ConfirmEmail";
        public const string ResendConfirmationEmail = $"{Prefix}/{RegisterController}/ResendConfirmationEmail";
        public const string ForgotPassword = $"{Prefix}/{PasswordController}/Forgot";
        public const string ResetPassword = $"{Prefix}/{PasswordController}/Reset";
        public const string TwoFactorAuth = $"{Prefix}/{ManagementController}/TwoFactorAuth";
        public const string GetInfo = $"{Prefix}/{ManagementController}/GetInfo";
        public const string PostInfo = $"{Prefix}/{ManagementController}/PostInfo";
    }
}
