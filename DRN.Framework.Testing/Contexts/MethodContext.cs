using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Testing.Contexts;

public class MethodContext(MethodInfo testMethod)
{
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

    internal void SetTestData(object[] dataRow)
    {
        Data = dataRow;
        SubstitutePairs = dataRow.GetSubstitutePairs();
    }

    internal void ReplaceSubstitutedInterfaces(IServiceCollection serviceCollection)
    {
        foreach (var grouping in SubstitutePairs.GroupBy(p => p.InterfaceType))
        {
            var type = grouping.Key;
            var implementations = grouping.Select(p => p.Implementation).ToArray();

            serviceCollection.ReplaceInstance(type, implementations, ServiceLifetime.Scoped);
        }
    }
}