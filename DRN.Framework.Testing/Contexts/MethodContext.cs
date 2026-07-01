using DRN.Framework.SharedKernel;
using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Testing.Contexts;

public class MethodContext(MethodInfo testMethod)
{
    private readonly object _tempPathLock = new();
    private string? _tempPath;
    private bool _isDeleted;

    public MethodInfo TestMethod { get; } = testMethod;
    public IReadOnlyList<object> Data { get; private set; } = [];
    public IReadOnlyList<SubstitutePair> SubstitutePairs { get; private set; } = [];

    public string GetTestFolderLocation()
    {
        var testClass = TestMethod.ReflectedType!;
        var assemblyName = testClass.Assembly.GetName().Name ?? "";
        var relativePathToTest = testClass.Namespace!.Remove(0, assemblyName.Length).TrimStart('.').Replace('.', Path.DirectorySeparatorChar);
        var testFolder = Path.Combine(Path.GetDirectoryName(testClass.Assembly.Location) ?? "", relativePathToTest);

        return testFolder;
    }

    public string GetTempPath()
    {
        if (_isDeleted)
            throw new ObjectDisposedException(nameof(MethodContext), "The test context has been disposed and the temporary path has been deleted.");

        if (_tempPath != null)
            return _tempPath;

        lock (_tempPathLock)
        {
            if (_isDeleted)
                throw new ObjectDisposedException(nameof(MethodContext), "The test context has been disposed and the temporary path has been deleted.");

            return _tempPath ??= CreateTempPath();
        }
    }

    internal void DeleteTempPath()
    {
        lock (_tempPathLock)
        {
            _isDeleted = true;

            if (string.IsNullOrWhiteSpace(_tempPath))
                return;

            if (Directory.Exists(_tempPath))
                Directory.Delete(_tempPath, true);

            _tempPath = null;
        }
    }

    private string CreateTempPath()
    {
        var testClass = TestMethod.ReflectedType;
        var testClassPath = (testClass?.FullName ?? TestMethod.Name).Replace('.', Path.DirectorySeparatorChar);
        var tempPath = Path.Combine(AppConstants.TempPath, testClassPath, TestMethod.Name, Guid.NewGuid().ToString("N"));

        if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
        Directory.CreateDirectory(tempPath);

        return tempPath;
    }

    internal void SetTestData(object[] dataRow)
    {
        Data = dataRow;
        SubstitutePairs = dataRow.GetSubstitutePairs();
    }

    internal void ReplaceSubstitutedInterfaces(IServiceCollection serviceCollection)
    {
        var containerCollection = serviceCollection.BuildServiceProvider().GetService<DrnServiceContainerCollection>();
        foreach (var grouping in SubstitutePairs.GroupBy(p => p.InterfaceType))
        {
            var type = grouping.Key;
            var implementations = grouping.Select(p => p.Implementation).ToArray();

            if (containerCollection == null || !containerCollection.ServiceTypeAndLifetimeMappings.TryGetValue(type, out var lifetime))
            {
                foreach (var implementation in implementations)
                    serviceCollection.AddScoped(type, _ => implementation);
                continue;
            }

            if (lifetime.TryAdd) //If not try-add that means service can have multiple implementations
                serviceCollection.ReplaceInstance(type, implementations, lifetime.ServiceLifetime);
            else // It is ok to not replace existing implementations
                foreach (var implementation in implementations)
                    serviceCollection.Add(new ServiceDescriptor(type, sp => implementation, lifetime.ServiceLifetime));
        }
    }
}
