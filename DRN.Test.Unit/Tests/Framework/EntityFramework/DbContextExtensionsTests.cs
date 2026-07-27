using DRN.Framework.EntityFramework.Extensions;

namespace DRN.Test.Unit.Tests.Framework.EntityFramework;

public class DbContextExtensionsTests
{
    [Theory]
    [DataInlineUnit("Acme.Data", "Acme.Data", true)]
    [DataInlineUnit("Acme.Data.Configurations", "Acme.Data", true)]
    [DataInlineUnit("Acme.Data.Configurations.Sub", "Acme.Data", true)]
    [DataInlineUnit("Acme.Database", "Acme.Data", false)]
    [DataInlineUnit("Acme.DataOther", "Acme.Data", false)]
    [DataInlineUnit("Other.Acme.Data", "Acme.Data", false)]
    [DataInlineUnit(null, "Acme.Data", false)]
    [DataInlineUnit("Acme.Data", null, false)]
    [DataInlineUnit(null, null, true)]
    public void IsExactOrChildNamespace_Should_Only_Accept_Exact_Or_True_Child_Namespaces(
        string? typeNamespace, string? contextNamespace, bool expected)
    {
        var result = DbContextExtensions.IsExactOrChildNamespace(typeNamespace, contextNamespace);
        result.Should().Be(expected);
    }
}
