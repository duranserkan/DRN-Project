using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils;
using DRN.Framework.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Testing;

/// <summary>
/// Test context that contains a slim Service Collection so that you can add your dependencies and build a service provider.
/// It disposes itself automatically at the end of the test.
/// </summary>
public sealed class TestContext : IDisposable
{
    public IReadOnlyList<object> Data { get; private set; } = null!;
    public IReadOnlyList<SubstitutePair> SubstitutePairs { get; private set; } = null!;
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
            var implementations = grouping.ToArray().Select(p => p.Implementation).ToArray();

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

    public void Dispose()
    {
        ServiceProvider?.Dispose();
        GC.SuppressFinalize(this);
    }
}