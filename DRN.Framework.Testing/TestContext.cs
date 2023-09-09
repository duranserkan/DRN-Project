using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Testing;

public class TestContext : IDisposable
{
    public ServiceCollection ServiceCollection { get; } = new();
    public ServiceProvider? ServiceProvider { get; private set; }

    /// <summary>
    /// Creates a service provider from test context service collection
    /// Init this within test method to capture name of the test and
    /// </summary>
    public IServiceProvider InitServiceProvider(string appSettingsName = "appsettings", [CallerMemberName] string nameOfTheTestMethod = "")
    {
        //dispose previous initiated sp to create new
        Dispose();
        ServiceProvider = ServiceCollection
            .AddSingleton(x => SettingsProvider.GetAppSettings(appSettingsName))
            .AddLogging()
            .BuildServiceProvider();
        return ServiceProvider;
    }

    public void Dispose()
    {
        ServiceProvider?.Dispose();
    }
}