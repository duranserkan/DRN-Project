using DRN.Framework.Testing.Providers;
using DRN.Framework.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Testing.Contexts;

/// <summary>
/// Test context that contains a slim Service Collection so that you can add your dependencies and build a service provider.
/// It disposes itself automatically at the end of the test.
/// </summary>
public sealed class TestContext : IDisposable, IServiceProvider
{
    private ServiceProvider? ServiceProvider { get; set; }
    public MethodContext MethodContext { get; } = new();
    public ServiceCollection ServiceCollection { get; } = new();


    //Todo: get data for test method
    //Todo: snipped and live template

    /// <summary>
    /// Creates a service provider from test context service collection
    /// Init this within test method to capture name of the test
    /// It includes logging and IAppSettings by default.
    /// More default services will be added by default as the drn framework develops
    /// </summary>
    public ServiceProvider BuildServiceProvider(string appSettingsName = "defaultAppSettings")
    {
        //dispose previously initiated sp to create new
        DisposeServiceProvider();
        MethodContext.ReplaceSubstitutedInterfaces(this);

        ServiceProvider = ServiceCollection
            .AddSingleton(x => SettingsProvider.GetAppSettings(appSettingsName))
            .AddSingleton(x => SettingsProvider.GetConfiguration(appSettingsName))
            .AddLogging()
            .AddDrnUtils()
            .BuildServiceProvider(false);
        return ServiceProvider;
    }

    internal void SetMethodInfo(MethodInfo testMethod) => MethodContext.SetMethodInfo(testMethod);

    internal void SetTestData(object[] data) => MethodContext.SetTestData(data);

    public override string ToString() => "context";

    public object? GetService(Type serviceType)
    {
        ServiceProvider ??= BuildServiceProvider();
        return ServiceProvider.GetService(serviceType);
    }

    public void Dispose()
    {
        DisposeServiceProvider();
        ServiceCollection.Clear();
        GC.SuppressFinalize(this);
    }

    private void DisposeServiceProvider()
    {
        ServiceProvider?.Dispose();
        ServiceProvider = null;
    }
}