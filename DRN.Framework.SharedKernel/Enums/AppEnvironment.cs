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