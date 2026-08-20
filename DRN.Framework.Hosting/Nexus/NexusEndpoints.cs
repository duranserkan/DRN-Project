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
        public const string UserPrefix = $"{User}";

        public const string LoginController = "NexusIdentityLogin";
        public const string RegisterController = "NexusIdentityRegister";
        public const string PasswordController = "NexusIdentityPassword";
        public const string ManagementController = "NexusIdentityManagement";

        public const string Login = $"{UserPrefix}/{LoginController}/Login";
        public const string Refresh = $"{UserPrefix}/{LoginController}/Refresh";
        public const string Register = $"{UserPrefix}/{RegisterController}/Register";
        public const string ConfirmEmail = $"{UserPrefix}/{RegisterController}/ConfirmEmail";
        public const string ResendConfirmationEmail = $"{UserPrefix}/{RegisterController}/ResendConfirmationEmail";
        public const string ForgotPassword = $"{UserPrefix}/{PasswordController}/Forgot";
        public const string ResetPassword = $"{UserPrefix}/{PasswordController}/Reset";
        public const string TwoFactorAuth = $"{UserPrefix}/{ManagementController}/TwoFactorAuth";
        public const string GetInfo = $"{UserPrefix}/{ManagementController}/GetInfo";
        public const string PostInfo = $"{UserPrefix}/{ManagementController}/PostInfo";
    }
}
