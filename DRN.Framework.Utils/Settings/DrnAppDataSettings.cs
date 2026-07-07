using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Settings;

[Config(errorOnUnknownConfiguration: false)]
public class DrnAppDataSettings
{
    /// <summary>
    /// App data root paths are resolved before DRN configuration is built. Set
    /// <c>DrnAppDataSettings__TempPath</c> and <c>DrnAppDataSettings__DataPath</c>
    /// as process environment variables; otherwise the system local application
    /// data location is used.
    /// </summary>
    public bool RequireTemp { get; init; }
    public bool RequireData { get; init; }
}
