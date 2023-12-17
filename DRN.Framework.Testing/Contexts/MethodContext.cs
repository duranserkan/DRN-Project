using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Testing.Contexts;

public class MethodContext
{
    public MethodContext(MethodInfo testMethod)
    {
        TestMethod = testMethod;
    }


    public MethodInfo TestMethod { get; }
    public IReadOnlyList<object> Data { get; private set; } = null!;
    public IReadOnlyList<SubstitutePair> SubstitutePairs { get; private set; } = null!;


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

    internal void ReplaceSubstitutedInterfaces(TestContext testContext)
    {
        foreach (var grouping in SubstitutePairs.GroupBy(p => p.InterfaceType))
        {
            var type = grouping.Key;
            var implementations = grouping.Select(p => p.Implementation).ToArray();

            testContext.ServiceCollection.ReplaceInstance(type, implementations, ServiceLifetime.Scoped);
        }
    }
}