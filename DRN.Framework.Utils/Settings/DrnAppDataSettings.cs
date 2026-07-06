using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Settings;

[Config]
public class DrnAppDataSettings
{
    public string? TempPath { get; init; }
    public string? DataPath { get; init; }
}
