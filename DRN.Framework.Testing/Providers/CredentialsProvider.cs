using System.Security.Cryptography;

namespace DRN.Framework.Testing.Providers;

/// <summary>
/// A helper class for generating and caching test usernames and passwords.
/// </summary>
public static class CredentialsProvider
{
    public const char Unique1 = 'Z';
    public const char Unique2 = 'y';

    //'Z' is ignored because it will seed as unique character
    public const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXY";

    //'y' is ignored because it will seed as unique character
    public const string Lowercase = "abcdefghijklmnopqrstuvwxz";
    public const string Digits = "0123456789";
    public const string Special = "!@#$%^&*()-_=+[]{}|;:,.<>?";
    public const string AllChars = Uppercase + Lowercase + Digits + Special;

    // Lazy initialization ensures thread-safe, lazy-loaded credentials.
    private static readonly Lazy<TestUserCredentials> TestUserCredentials = new(GenerateCredentials, isThreadSafe: true);

    /// <summary>
    /// Gets the cached test user credentials.
    /// </summary>
    public static TestUserCredentials Credentials => TestUserCredentials.Value;

    /// <summary>
    /// Generates a unique username and a secure password.
    /// </summary>
    public static TestUserCredentials GenerateCredentials()
    {
        var username = GenerateUniqueUsername();
        var password = GenerateSecurePassword(12);
        return new TestUserCredentials(username, password);
    }

    private static string GenerateUniqueUsername() => $"testuser_{Guid.NewGuid():N}";

    /// <summary>
    /// Generates a secure password containing uppercase, lowercase, digits, and special characters.
    /// </summary>
    /// <param name="length">Desired length of the password (minimum 8).</param>
    /// <returns>A secure password string.</returns>
    private static string GenerateSecurePassword(int length)
    {
        if (length < 8)
            throw new ArgumentException("Password length should be at least 8 characters.", nameof(length));

        var passwordChars = new char[length];
        var randomBytes = new byte[length];

        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(randomBytes);

        for (var i = 0; i < length; i++)
            passwordChars[i] = AllChars[randomBytes[i] % AllChars.Length];

        // Ensure the password contains at least one character from each category
        passwordChars[0] = Uppercase[randomBytes[0] % Uppercase.Length];
        passwordChars[1] = Lowercase[randomBytes[1] % Lowercase.Length];
        passwordChars[2] = Digits[randomBytes[2] % Digits.Length];
        passwordChars[3] = Special[randomBytes[3] % Special.Length];
        passwordChars[4] = Unique1;
        passwordChars[5] = Unique2;

        // Shuffle the characters to prevent predictable sequences
        return ShuffleString(new string(passwordChars));
    }

    /// <summary>
    /// Shuffles the characters in a string randomly.
    /// </summary>
    /// <param name="input">The string to shuffle.</param>
    /// <returns>A shuffled string.</returns>
    private static string ShuffleString(string input)
    {
        var array = input.ToCharArray();
        var n = array.Length;
        using (var rng = RandomNumberGenerator.Create())
            while (n > 1)
            {
                var box = new byte[1];
                do
                {
                    rng.GetBytes(box);
                } while (box[0] >= n * (byte.MaxValue / n));

                var k = box[0] % n;
                n--;
                (array[k], array[n]) = (array[n], array[k]);
            }

        return new string(array);
    }
}

public record TestUserCredentials(string Username, string Password)
{
    public string EmailAddress => $"{Username}@example.com";
}