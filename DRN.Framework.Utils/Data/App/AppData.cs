using DRN.Framework.SharedKernel.Extensions;
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
    public DrnAppDataSettings Settings { get; }
    public AppDataPathResult Temp { get; }
    public AppDataPathResult Data { get; }

    public AppData(DrnAppDataSettings settings)
        : this(settings, AppConstants.TempPath, AppConstants.LocalAppDataPath)
    {
    }

    internal AppData(DrnAppDataSettings settings, string fallbackTempPath, string fallbackLocalAppDataPath)
    {
        Settings = settings;
        _ = TestEnvironment.DrnTestContextEnabled
            ? fallbackTempPath.TryCreateDirectory()
            : fallbackTempPath.TryRecreateDirectory(); //clean up on each start

        fallbackLocalAppDataPath.TryCreateDirectory();

        Temp = AppDataPathResult.From(fallbackTempPath);
        Data = AppDataPathResult.From(fallbackLocalAppDataPath);

        if (settings.RequireTemp && Temp.Status != AppDataPathStatus.Valid)
            throw ExceptionFor.Configuration($"Temp path '{Temp.Path}' is required but not valid. Status: {Temp.Status}");

        if (settings.RequireData && Data.Status != AppDataPathStatus.Valid)
            throw ExceptionFor.Configuration($"Data path '{Data.Path}' is required but not valid. Status: {Data.Status}");
    }
}