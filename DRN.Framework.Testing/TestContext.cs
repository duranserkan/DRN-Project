using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Testing;

/// <summary>
/// Test context that contains a slim Service Collection so that you can add your dependencies and build a service provider.
/// It disposes itself automatically at the end of the test.
/// </summary>
public sealed class TestContext : IDisposable
{
    //Todo:Will be used later with new features to improve integration test dependency isolation.
    public MethodInfo TestMethod { get; private set; } = null!;
    public ServiceCollection ServiceCollection { get; } = new();
    public ServiceProvider? ServiceProvider { get; private set; }

    /// <summary>
    /// Creates a service provider from test context service collection
    /// Init this within test method to capture name of the test
    /// It includes logging and IAppSettings by default.
    /// More default services will be added by default as the drn framework develops
    /// </summary>
    public IServiceProvider BuildServiceProvider(string appSettingsName = "appsettings")
    {
        //dispose previous initiated sp to create new
        Dispose();
        ServiceProvider = ServiceCollection
            .AddSingleton(x => SettingsProvider.GetAppSettings(appSettingsName))
            .AddLogging()
            .BuildServiceProvider();
        return ServiceProvider;
    }


    internal void SetMethodInfo(MethodInfo testMethod) => TestMethod = testMethod;
    public override string ToString() => "context";
    public void Dispose()
    {
        ServiceProvider?.Dispose();
        GC.SuppressFinalize(this);
    }
}