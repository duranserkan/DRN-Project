using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Data.App;

public interface IAppData
{
    AppDataPathResult Temp { get; }
    AppDataPathResult Data { get; }
}

[Singleton<IAppData>]
public class AppData(DrnAppDataSettings settings) : IAppData
{
    public AppDataPathResult Temp { get; } = Resolve(settings.TempPath, AppConstants.TempPath);
    public AppDataPathResult Data { get; } = ResolveData(settings.DataPath, AppConstants.LocalAppDataPath);

    private static AppDataPathResult Resolve(string? configuredPath, string fallback)
    {
        var rawPath = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : fallback;

        return AppDataPathResult.From(rawPath);
    }

    private static AppDataPathResult ResolveData(string? configuredPath, string fallback)
    {
        var result = Resolve(configuredPath, fallback);
        if (result.Status != AppDataPathStatus.PathNotFound)
            return result;

        try
        {
            Directory.CreateDirectory(result.Path);
        }
        catch (Exception)
        {
            return result;
        }

        return AppDataPathResult.From(result.Path);
    }
}
