using DRN.Framework.Testing.Extensions;
using DRN.Framework.Testing.Providers;
using DRN.Framework.Utils;
using DRN.Framework.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Testing;

/// <summary>
/// Test context that contains a slim Service Collection so that you can add your dependencies and build a service provider.
/// It disposes itself automatically at the end of the test.
/// </summary>

public sealed class TestContext : IDisposable, IServiceProvider
{
    public IReadOnlyList<object> Data { get; private set; } = null!;
    public IReadOnlyList<SubstitutePair> SubstitutePairs { get; private set; } = null!;
    public MethodInfo TestMethod { get; private set; } = null!;
    public ServiceCollection ServiceCollection { get; } = new();
    private ServiceProvider? ServiceProvider { get; set; }

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
        ReplaceSubstitutedInterfaces();

        ServiceProvider = ServiceCollection
            .AddSingleton(x => SettingsProvider.GetAppSettings(appSettingsName))
            .AddSingleton(x => SettingsProvider.GetConfiguration(appSettingsName))
            .AddLogging()
            .AddDrnUtils()
            .BuildServiceProvider(false);
        return ServiceProvider;
    }

    private void ReplaceSubstitutedInterfaces()
    {
        foreach (var grouping in SubstitutePairs.GroupBy(p => p.InterfaceType))
        {
            var type = grouping.Key;
            var implementations = grouping.Select(p => p.Implementation).ToArray();

            ServiceCollection.ReplaceInstance(type, implementations, ServiceLifetime.Scoped);
        }
    }

    internal void SetMethodInfo(MethodInfo testMethod) => TestMethod = testMethod;

    internal void SetTestData(object[] data)
    {
        Data = data;
        SubstitutePairs = data.GetSubstitutePairs();
    }

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