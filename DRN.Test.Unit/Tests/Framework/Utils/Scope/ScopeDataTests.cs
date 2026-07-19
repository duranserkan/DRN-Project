using DRN.Framework.Utils.Scope;

namespace DRN.Test.Unit.Tests.Framework.Utils.Scope;

public class ScopeDataTests
{
    [Fact]
    public void ScopeData_Should_Store_CallerOwned_Flags_And_Parameters()
    {
        var data = new ScopeData();

        data.SetFlag("feature", true);
        data.SetParameter("limit", 41);

        data.IsFlagEnabled("FEATURE").Should().BeTrue();
        data.GetParameter<int>("LIMIT").Should().Be(41);
    }
}
