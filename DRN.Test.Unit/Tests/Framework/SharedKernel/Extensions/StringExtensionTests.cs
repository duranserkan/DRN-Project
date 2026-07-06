using System.Globalization;
using DRN.Framework.SharedKernel.Extensions;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Extensions;

public class StringExtensionTests
{
    [Theory]
    [DataInlineUnit("George Carlin: Everyone smiles in the same Language", "george_carlin_everyone_smiles_in_the_same_language")]
    public void ToSnakeCaseTest(string input, string output)
    {
        input.ToSnakeCase().Should().Be(output);
    }

    [Theory]
    [DataInlineUnit("George Carlin: Everyone smiles in the same Language", "georgeCarlinEveryoneSmilesInTheSameLanguage")]
    public void ToCamelCase(string input, string output)
    {
        input.ToCamelCase().Should().Be(output);
    }

    [Theory]
    [DataInlineUnit("George Carlin: Everyone smiles in the same Language", "GeorgeCarlinEveryoneSmilesInTheSameLanguage")]
    public void ToPascalCase(string input, string output)
    {
        input.ToPascalCase().Should().Be(output);
    }

    [Fact]
    public void CasingExtensions_ShouldUseInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var turkishCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentCulture = turkishCulture;
            CultureInfo.CurrentUICulture = turkishCulture;

            "IDENTITY information".ToCamelCase().Should().Be("identityInformation");
            "identity information".ToPascalCase().Should().Be("IdentityInformation");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
