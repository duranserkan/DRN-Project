namespace DRN.Test.Unit.Tests.Framework.Utils.Extensions;

public class DictionaryExtensionsTests
{
    [Fact]
    public void UpdateIf_WhenKeyExistsAndConditionIsTrue_ShouldUpdateValue()
    {
        var dictionary = new Dictionary<string, int>
        {
            ["item1"] = 10,
            ["item2"] = 20
        };

        dictionary.UpdateIf(newValue: 99, key: "item1", condition: v => v == 10);

        dictionary["item1"].Should().Be(99);
        dictionary["item2"].Should().Be(20);
    }

    [Fact]
    public void UpdateIf_WhenKeyExistsAndConditionIsFalse_ShouldNotUpdateValue()
    {
        var dictionary = new Dictionary<string, int>
        {
            ["item1"] = 10
        };

        dictionary.UpdateIf(newValue: 99, key: "item1", condition: v => v > 50);

        dictionary["item1"].Should().Be(10);
    }

    [Fact]
    public void UpdateIf_WhenKeyDoesNotExist_ShouldNotAddKeyOrInvokeCondition()
    {
        var dictionary = new Dictionary<string, int>
        {
            ["item1"] = 10
        };
        var conditionInvoked = false;

        dictionary.UpdateIf(newValue: 99, key: "nonexistent", condition: _ =>
        {
            conditionInvoked = true;
            return true;
        });

        conditionInvoked.Should().BeFalse();
        dictionary.ContainsKey("nonexistent").Should().BeFalse();
        dictionary.Count.Should().Be(1);
    }

    [Fact]
    public void GetAndCastValueOrDefault_WhenKeyExistsWithMatchingType_ShouldReturnValue()
    {
        var dictionary = new Dictionary<string, object?>
        {
            ["number"] = 42,
            ["text"] = "hello"
        };

        var numberResult = dictionary.GetAndCastValueOrDefault<string, int>("number");
        var textResult = dictionary.GetAndCastValueOrDefault<string, string>("text");

        numberResult.Should().Be(42);
        textResult.Should().Be("hello");
    }

    [Fact]
    public void GetAndCastValueOrDefault_WhenKeyExistsWithDifferentType_ShouldReturnDefaultValue()
    {
        var dictionary = new Dictionary<string, object?>
        {
            ["number"] = 42
        };

        var result = dictionary.GetAndCastValueOrDefault("number", defaultValue: "fallback");

        result.Should().Be("fallback");
    }

    [Fact]
    public void GetAndCastValueOrDefault_WhenKeyDoesNotExist_ShouldReturnDefaultValue()
    {
        var dictionary = new Dictionary<string, object?>
        {
            ["number"] = 42
        };

        var result = dictionary.GetAndCastValueOrDefault("missingKey", defaultValue: -1);

        result.Should().Be(-1);
    }
}
