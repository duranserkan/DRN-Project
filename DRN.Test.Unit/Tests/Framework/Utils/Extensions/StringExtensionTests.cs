namespace DRN.Test.Unit.Tests.Framework.Utils.Extensions;

public class StringExtensionTests
{
    [Theory]
    [InlineData("George Carlin: Everyone smiles in the same Language", "george_carlin_everyone_smiles_in_the_same_language")]
    public void ToSnakeCaseTest(string input, string output)
    {
        input.ToSnakeCase().Should().Be(output);
    }
    
    [Theory]
    [InlineData("George Carlin: Everyone smiles in the same Language", "georgeCarlinEveryoneSmilesInTheSameLanguage")]
    public void ToCamelCase(string input, string output)
    {
        input.ToCamelCase().Should().Be(output);
    }
    
    [Theory]
    [InlineData("George Carlin: Everyone smiles in the same Language", "GeorgeCarlinEveryoneSmilesInTheSameLanguage")]
    public void ToPascalCase(string input, string output)
    {
        input.ToPascalCase().Should().Be(output);
    }
}