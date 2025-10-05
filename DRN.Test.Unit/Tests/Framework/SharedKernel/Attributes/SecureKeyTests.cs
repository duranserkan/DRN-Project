using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.Data.Validation;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Attributes;

public class ToBeValidated
{
    [SecureKey]
    public required string Key { get; init; }
}

public class SecureKeyTests
{
    [Theory]
    [DataMemberUnit(nameof(TestCases))]
    public void SecureKey_Validation_Should_Return_Expected_Result(string key, bool expected)
    {
        var obj = new ToBeValidated
        {
            Key = key
        };

        var result = obj.ValidateDataAnnotations();

        result.IsValid.Should().Be(expected);
    }

    public static IEnumerable<object[]> TestCases => new List<object[]>
    {
        //✅ Valid:
        // Minimal valid key (16 chars) with all required character classes
        new object[] { "Aa1!Aa1!Aa1!Aa1!", true },
        // 17-character valid key
        new object[] { "Aa1!Aa1!Aa1!Aa1!A", true },
        // Maximum length (128 chars) valid key
        new object[] { "Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!", true },
        // Mixed-case non-ASCII-sequential letters (allowed by design)
        new object[] { "AbCdEfGhIjKlMnOp1!", true },
        // Repeated characters within limit (3 max)
        new object[] { "AAAaaa111!!!AAaa11!!", true },
        // Ends with allowed special character '-'
        new object[] { "Aa1!Aa1!Aa1!Aa1-", true },
        // Starts with allowed special character '_'
        new object[] { "_Aa1!Aa1!Aa1!Aa1", true },
        // Sequential runs of exactly 4 (max allowed) — should pass
        new object[] { "ABCD1234Aa!Aa!Aa", true },
        // Descending sequential runs of exactly 4 — should pass
        new object[] { "DCBA4321Aa!Aa!Aa!", true },
        // Contains allowed space character
        new object[] { "Aa1 Aa1 Aa1 Aa1 ", true },

        // ❌ INVALID:
        // Contains disallowed special characters: @, #, $, %, ^
        new object[] { "A1!b2@c3#d4$e5%f6^", false },
        // Too short (15 characters); minimum is 16
        new object[] { "Aa1!Aa1!Aa1!Aa1", false },
        // Too long (129 characters); maximum is 128
        new object[] { "Aa1!Aa1!Aa1!Aa1!X".PadRight(129, 'X'), false },
        // Missing uppercase, digit, and special character
        new object[] { "aaaaaaaaaaaaaaaa", false },
        // Missing lowercase, digit, and special character
        new object[] { "AAAAAAAAAAAAAAAA", false },
        // Missing uppercase, lowercase, and special character
        new object[] { "1111111111111111", false },
        // Missing uppercase, lowercase, and digit
        new object[] { "!!!!!!!!!!!!!!!!", false },
        // Too short (10 characters)
        new object[] { "AAAaa11!!", false },
        // Contains ascending letter sequence of length 5 (> MaxSequentialChars=4)
        new object[] { "ABCDEabcd12345!*", false },
        // Contains descending digit sequence of length 5 (> MaxSequentialChars=4)
        new object[] { "54321abcde!*()-", false },
        // Four identical 'A' characters in a row (> MaxRepeatedChars=3)
        new object[] { "AAAAaa111!!Aa1!A", false },
        // Four '!' characters in a row (> MaxRepeatedChars=3)
        new object[] { "Aa1!!!!Aa1!!!Aa1", false },
        // Null input
        new object[] { null, false },
        // Empty string
        new object[] { "", false },
        // Too short (4 characters)
        new object[] { "Aa1!", false },
        // Missing lowercase, digit, and special character
        new object[] { "A".PadRight(16, 'A'), false },
        // Missing uppercase, digit, and special character
        new object[] { "a".PadRight(16, 'a'), false },
        // Missing uppercase, lowercase, and special character
        new object[] { "1".PadRight(16, '1'), false },
        // Missing uppercase, lowercase, and digit
        new object[] { "!".PadRight(16, '!'), false },
        // Contains disallowed character '@'
        new object[] { "Aa1@Aa1@Aa1@Aa1@", false },
        // Contains disallowed newline character
        new object[] { "Aa1\nAa1\nAa1\nAa1\n", false },
        // Contains disallowed tab character
        new object[] { "Aa1\tAa1\tAa1\tAa1\t", false },
        // Contains disallowed double quote character
        new object[] { "Aa1\"Aa1\"Aa1\"Aa1\"", false },
        // Contains disallowed apostrophe character
        new object[] { "Aa1'Aa1'Aa1'Aa1'", false },
        // Contains disallowed '+' character
        new object[] { "Aa1+Aa1+Aa1+Aa1+", false },
        // Contains disallowed '=' character
        new object[] { "Aa1=Aa1=Aa1=Aa1=", false },
        // Contains disallowed '?' character
        new object[] { "Aa1?Aa1?Aa1?Aa1?", false },
        // Contains disallowed '~' character
        new object[] { "Aa1~Aa1~Aa1~Aa1~", false },
        // Descending letter and digit sequences of length 5 (>4)
        new object[] { "EDCBA54321Aa!Aa!", false },
        // Four identical characters in a row for multiple classes (> MaxRepeatedChars=3)
        new object[] { "AAAABBBB1111!!!!", false }
    };
}