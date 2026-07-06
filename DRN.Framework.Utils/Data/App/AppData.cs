using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Data.App;

public interface IAppData
{
    AppDataPathResult Temp { get; }
    AppDataPathResult Data { get; }
}

[Singleton<IAppData>]
public class AppData : IAppData
{
    public AppDataPathResult Temp { get; }
    public AppDataPathResult Data { get; }

    public AppData(DrnAppDataSettings settings)
        : this(settings, AppConstants.TempPath, AppConstants.LocalAppDataPath)
    {
    }

    internal AppData(DrnAppDataSettings settings, string fallbackTempPath, string fallbackLocalAppDataPath)
    {
        Temp = ResolveTemp(settings.TempPath, fallbackTempPath);
        Data = ResolveData(settings.DataPath, fallbackLocalAppDataPath);
    }

    private static AppDataPathResult Resolve(string? configuredPath, string fallback)
    {
        var rawPath = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : fallback;

        return AppDataPathResult.From(rawPath);
    }

    private static AppDataPathResult ResolveTemp(string? configuredPath, string fallback)
    {
        var temp = Resolve(configuredPath, fallback);
        try
        {
            if (Directory.Exists(temp.Path))
                Directory.Delete(temp.Path, true);

            Directory.CreateDirectory(temp.Path);
        }
        catch (Exception)
        {
            // ignored
        }

        temp = Resolve(configuredPath, fallback);

        return temp;
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