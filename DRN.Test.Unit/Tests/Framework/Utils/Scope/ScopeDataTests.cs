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

    [Fact]
    public void GetParameter_Should_Handle_Missing_Incompatible_And_StoredNull_Parameters()
    {
        var data = new ScopeData();

        data.SetParameter<string?>("nullRefKey", null);
        data.SetParameter<int?>("nullValKey", null);
        data.SetParameter("mismatchedKey", 123);

        // Missing key returns defaultValue
        data.GetParameter("missingKey", "fallback").Should().Be("fallback");
        data.GetParameter("missingKey", 99).Should().Be(99);

        // Incompatible stored type returns defaultValue
        data.GetParameter("mismatchedKey", "fallback").Should().Be("fallback");

        // Explicitly stored null returns null for nullable reference & nullable value types
        data.GetParameter<string?>("nullRefKey", "fallback").Should().BeNull();
        data.GetParameter<int?>("nullValKey", 99).Should().BeNull();

        // Explicitly stored null returns defaultValue for non-nullable value types
        data.GetParameter("nullRefKey", 42).Should().Be(42);
    }
}
