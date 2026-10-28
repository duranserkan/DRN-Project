using Xunit.Sdk;

namespace DRN.Test.Integration.Helpers;

// These hosts do not need the integration startup job that provisions Sample/Nexus databases.
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class IsolatedHostingDataAttribute(params object?[] values) : DataSelfAttribute
{
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var context = new DrnTestContext(testMethod, triggerStartUp: false);
        disposalTracker.Add(context);
        object?[] data = [context, .. values];
        context.MethodContext.SetTestData(data!);
        return ValueTask.FromResult<IReadOnlyCollection<ITheoryDataRow>>([new TheoryDataRow(data)]);
    }

    public override bool SupportsDiscoveryEnumeration() => false;
}
