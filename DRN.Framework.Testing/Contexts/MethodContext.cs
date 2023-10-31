using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Testing.Contexts;

public class MethodContext
{
    public IReadOnlyList<object> Data { get; private set; } = null!;
    public IReadOnlyList<SubstitutePair> SubstitutePairs { get; private set; } = null!;
    public MethodInfo TestMethod { get; private set; } = null!;

    internal void ReplaceSubstitutedInterfaces(TestContext testContext)
    {
        foreach (var grouping in SubstitutePairs.GroupBy(p => p.InterfaceType))
        {
            var type = grouping.Key;
            var implementations = grouping.Select(p => p.Implementation).ToArray();

            testContext.ServiceCollection.ReplaceInstance(type, implementations, ServiceLifetime.Scoped);
        }
    }

    internal void SetMethodInfo(MethodInfo testMethod) => TestMethod = testMethod;

    internal void SetTestData(object[] data)
    {
        Data = data;
        SubstitutePairs = data.GetSubstitutePairs();
    }
}