using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DRN.Framework.Utils.Data.Encodings;

namespace DRN.Framework.Utils.Auth.MFA;

/// <summary>
/// Generates and verifies RFC 6238 time-based one-time passwords backed by RFC 4648 Base32 secrets.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class TotpUtils
{
    public const int DefaultDigits = 6;
    public const int DefaultTimeStepSeconds = 30;
    public const int DefaultAllowedTimeStepDrift = 1;
    public const int MaxAllowedTimeStepDrift = 10;

    /// <summary>
    /// Generates a TOTP code using the current UTC time.
    /// </summary>
    public static string GenerateTotpCode(string sharedKey) =>
        GenerateTotpCode(sharedKey, TimeProvider.System.GetUtcNow());

    /// <summary>
    /// Generates a TOTP code for an explicit timestamp.
    /// </summary>
    public static string GenerateTotpCode(
        string sharedKey,
        DateTimeOffset timestamp,
        int digits = DefaultDigits,
        int timeStepSeconds = DefaultTimeStepSeconds)
    {
        ValidateTimestamp(timestamp);
        ValidateSettings(digits, timeStepSeconds);

        var secret = DecodeSharedKey(sharedKey);
        try
        {
            var counter = timestamp.ToUnixTimeSeconds() / timeStepSeconds;
            return GenerateTotpCode(secret, counter, digits);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <summary>
    /// Verifies a TOTP code using the current UTC time and the default adjacent-step allowance.
    /// Note: Verification is stateless and does not prevent code reuse within the allowed drift window.
    /// Callers must track the last accepted counter or code per user to enforce single-use validation.
    /// </summary>
    public static bool VerifyTotpCode(string sharedKey, string code) =>
        VerifyTotpCode(sharedKey, code, TimeProvider.System.GetUtcNow());

    /// <summary>
    /// Verifies a TOTP code for an explicit timestamp and bounded time-step drift.
    /// Note: Verification is stateless and does not prevent code reuse within the allowed drift window.
    /// Callers must track the last accepted counter or code per user to enforce single-use validation.
    /// </summary>
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "RFC 6238 authenticator interoperability requires the HMAC-SHA1 profile.")]
    [SuppressMessage("SonarQube", "S4790",
        Justification = "RFC 6238 authenticator interoperability requires the HMAC-SHA1 profile.")]
    public static bool VerifyTotpCode(
        string sharedKey,
        string code,
        DateTimeOffset timestamp,
        int allowedTimeStepDrift = DefaultAllowedTimeStepDrift,
        int digits = DefaultDigits,
        int timeStepSeconds = DefaultTimeStepSeconds)
    {
        ArgumentNullException.ThrowIfNull(code);
        ValidateTimestamp(timestamp);
        ValidateSettings(digits, timeStepSeconds);

        if (allowedTimeStepDrift is < 0 or > MaxAllowedTimeStepDrift)
            throw new ArgumentOutOfRangeException(nameof(allowedTimeStepDrift), allowedTimeStepDrift,
                $"Allowed time-step drift must be between 0 and {MaxAllowedTimeStepDrift}.");

        if (code.Length != digits || code.Any(character => character is < '0' or > '9'))
            return false;

        var secret = DecodeSharedKey(sharedKey);
        try
        {
            var currentCounter = timestamp.ToUnixTimeSeconds() / timeStepSeconds;
            Span<byte> providedCode = stackalloc byte[8];
            Span<byte> candidateCode = stackalloc byte[8];
            Encoding.ASCII.GetBytes(code, providedCode);

            using var hmac = new HMACSHA1(secret);
            var verified = false;
            for (var offset = -allowedTimeStepDrift; offset <= allowedTimeStepDrift; offset++)
            {
                var counter = currentCounter + offset;
                if (counter < 0)
                    continue;

                var candidate = GenerateTotpCode(hmac, counter, digits);
                Encoding.ASCII.GetBytes(candidate, candidateCode);
                verified |= CryptographicOperations.FixedTimeEquals(providedCode[..digits], candidateCode[..digits]);
            }

            return verified;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "RFC 6238 authenticator interoperability requires the HMAC-SHA1 profile.")]
    [SuppressMessage("SonarQube", "S4790",
        Justification = "RFC 6238 authenticator interoperability requires the HMAC-SHA1 profile.")]
    private static string GenerateTotpCode(byte[] secret, long counter, int digits)
    {
        using var hmac = new HMACSHA1(secret);
        return GenerateTotpCode(hmac, counter, digits);
    }

    private static string GenerateTotpCode(HMACSHA1 hmac, long counter, int digits)
    {
        Span<byte> counterBytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        Span<byte> hash = stackalloc byte[20];
        if (!hmac.TryComputeHash(counterBytes, hash, out var bytesWritten) || bytesWritten != hash.Length)
            throw new CryptographicException("Unable to compute the RFC 6238 HMAC-SHA1 value.");

        var offset = hash[^1] & 0x0f;
        var binaryCode = BinaryPrimitives.ReadInt32BigEndian(hash[offset..]) & 0x7fff_ffff;
        var modulus = digits switch
        {
            6 => 1_000_000,
            7 => 10_000_000,
            8 => 100_000_000,
            _ => throw new ArgumentOutOfRangeException(nameof(digits))
        };

        return (binaryCode % modulus).ToString($"D{digits}", CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeSharedKey(string sharedKey)
    {
        ArgumentNullException.ThrowIfNull(sharedKey);
        return sharedKey.Length != 0
            ? Base32Encoding.Decode(sharedKey)
            : throw new FormatException("The Base32 shared key cannot be empty.");
    }

    private static void ValidateTimestamp(DateTimeOffset timestamp)
    {
        if (timestamp < DateTimeOffset.UnixEpoch)
            throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, "The TOTP timestamp cannot precede the Unix epoch.");
    }

    private static void ValidateSettings(int digits, int timeStepSeconds)
    {
        if (digits is < 6 or > 8)
            throw new ArgumentOutOfRangeException(nameof(digits), digits, "TOTP digits must be between 6 and 8.");
        if (timeStepSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeStepSeconds), timeStepSeconds, "The TOTP time step must be positive.");
    }
}
