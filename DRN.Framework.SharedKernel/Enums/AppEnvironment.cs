namespace DRN.Framework.SharedKernel.Enums;

/// <summary>
///  Read from configuration with Environment key.
///  Uses Same naming with Microsoft.Extensions.Hosting.Environments.
/// </summary>
public enum AppEnvironment
{
    NotDefined = 0,
    Development,
    Staging,
    Production
}

//namespace Microsoft.Extensions.Hosting
//{
//    /// <summary>Commonly used environment names.</summary>
//    public static class Environments
//    {
//        /// <summary>Specifies the Development environment.</summary>
//        /// <remarks>The development environment can enable features that shouldn't be exposed in production. Because of the performance cost, scope validation and dependency validation only happens in development.</remarks>
//        public static readonly string Development = nameof(Development);
//
//        /// <summary>Specifies the Staging environment.</summary>
//        /// <remarks>The staging environment can be used to validate app changes before changing the environment to production.</remarks>
//        public static readonly string Staging = nameof(Staging);
//
//        /// <summary>Specifies the Production environment.</summary>
//        /// <remarks>The production environment should be configured to maximize security, performance, and application robustness.</remarks>
//        public static readonly string Production = nameof(Production);
//    }
//}