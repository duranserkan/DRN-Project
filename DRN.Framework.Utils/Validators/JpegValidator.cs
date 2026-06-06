using DRN.Framework.Utils.Data.Serialization;

namespace DRN.Framework.Utils.Validators;

public static class JpegValidator
{
    private const string InvalidJpegMessage = "JPEG image data is invalid.";
    private const string InvalidMaxLengthMessage = "Maximum length must be greater than or equal to zero.";
    private const byte MarkerPrefix = 0xFF;
    private const byte StuffedMarker = 0x00;
    private const byte StartOfImage = 0xD8;
    private const byte EndOfImage = 0xD9;
    private const byte StartOfScan = 0xDA;

    public static bool IsValid(ReadOnlySpan<byte> imageData)
        => IsValid(imageData, long.MaxValue);

    public static async ValueTask<bool> IsValidAsync(Stream imageStream, long maxLength = long.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var result = await ValidateAsync(imageStream, maxLength, cancellationToken);
        return result.IsValid;
    }

    public static JpegValidationResult Validate(byte[] imageData, long maxLength = long.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(imageData);

        if (maxLength < 0)
            return JpegValidationResult.Invalid(InvalidMaxLengthMessage, JpegValidationErrorReason.InvalidMaxLength);

        if (imageData.Length > maxLength)
            return JpegValidationResult.Invalid(
                $"JPEG image data exceeds the maximum allowed length of {maxLength:N0} bytes.",
                JpegValidationErrorReason.MaxLengthExceeded);

        return IsValid(imageData, maxLength)
            ? JpegValidationResult.Valid(imageData)
            : JpegValidationResult.Invalid(InvalidJpegMessage);
    }

    public static async ValueTask<JpegValidationResult> ValidateAsync(Stream imageStream, long maxLength = long.MaxValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        if (maxLength < 0)
            return JpegValidationResult.Invalid(InvalidMaxLengthMessage, JpegValidationErrorReason.InvalidMaxLength);

        try
        {
            var imageData = await imageStream.ToArrayAsync(maxLength, cancellationToken);
            return Validate(imageData, maxLength);
        }
        catch (ValidationException exception)
        {
            return JpegValidationResult.Invalid(exception.Message, JpegValidationErrorReason.MaxLengthExceeded);
        }
    }

    public static bool IsValid(ReadOnlySpan<byte> imageData, long maxLength)
    {
        if (maxLength < 0 || imageData.Length > maxLength || imageData.Length < 4
            || imageData[0] != MarkerPrefix || imageData[1] != StartOfImage)
            return false;

        var index = 2;
        var hasFrame = false;
        var hasScan = false;
        byte frameComponentCount = 0;

        while (index < imageData.Length)
        {
            if (!TryReadMarker(imageData, ref index, out var marker))
                return false;

            if (marker == EndOfImage)
                return hasFrame && hasScan && HasOnlyTrailingPadding(imageData[index..]);

            if (IsStandaloneMarker(marker))
                continue;

            if (!TryReadSegment(imageData, ref index, out var segment))
                return false;

            if (IsStartOfFrame(marker))
            {
                if (!TryReadFrameComponentCount(segment, out frameComponentCount))
                    return false;

                hasFrame = true;
            }
            else if (marker == StartOfScan)
            {
                if (!hasFrame || !HasValidScanHeader(segment, frameComponentCount))
                    return false;

                hasScan = true;
                if (!TrySkipScanData(imageData, ref index))
                    return false;
            }
        }

        return false;
    }

    private static bool TryReadMarker(ReadOnlySpan<byte> imageData, ref int index, out byte marker)
    {
        marker = 0;
        if (index >= imageData.Length || imageData[index] != MarkerPrefix)
            return false;

        while (index < imageData.Length && imageData[index] == MarkerPrefix)
            index++;

        if (index >= imageData.Length)
            return false;

        marker = imageData[index++];
        return marker != StuffedMarker;
    }

    private static bool TryReadSegment(ReadOnlySpan<byte> imageData, ref int index, out ReadOnlySpan<byte> segment)
    {
        segment = default;
        if (index + 2 > imageData.Length)
            return false;

        var length = (imageData[index] << 8) | imageData[index + 1];
        if (length < 2 || index + length > imageData.Length)
            return false;

        segment = imageData.Slice(index + 2, length - 2);
        index += length;
        return true;
    }

    private static bool TrySkipScanData(ReadOnlySpan<byte> imageData, ref int index)
    {
        var hasEntropyData = false;
        while (index < imageData.Length)
        {
            if (imageData[index++] != MarkerPrefix)
            {
                hasEntropyData = true;
                continue;
            }

            var markerStart = index - 1;
            while (index < imageData.Length && imageData[index] == MarkerPrefix)
                index++;

            if (index >= imageData.Length)
                return false;

            var marker = imageData[index];
            if (marker == StuffedMarker || IsRestartMarker(marker))
            {
                hasEntropyData = true;
                index++;
                continue;
            }

            index = markerStart;
            return hasEntropyData;
        }

        return false;
    }

    private static bool IsStandaloneMarker(byte marker)
        => marker == 0x01 || IsRestartMarker(marker);

    private static bool IsRestartMarker(byte marker)
        => marker is >= 0xD0 and <= 0xD7;

    private static bool IsStartOfFrame(byte marker)
        => marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7
            or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;

    private static bool TryReadFrameComponentCount(ReadOnlySpan<byte> segment, out byte componentCount)
    {
        componentCount = 0;
        if (segment.Length < 6)
            return false;

        var precision = segment[0];
        var height = (segment[1] << 8) | segment[2];
        var width = (segment[3] << 8) | segment[4];
        componentCount = segment[5];

        return precision > 0
               && height > 0
               && width > 0
               && componentCount is >= 1 and <= 4
               && segment.Length == 6 + componentCount * 3;
    }

    private static bool HasValidScanHeader(ReadOnlySpan<byte> segment, byte frameComponentCount)
    {
        if (segment.Length < 4)
            return false;

        var scanComponentCount = segment[0];
        return scanComponentCount is >= 1 and <= 4
               && scanComponentCount <= frameComponentCount
               && segment.Length == 4 + scanComponentCount * 2;
    }

    private static bool HasOnlyTrailingPadding(ReadOnlySpan<byte> imageData)
    {
        foreach (var item in imageData)
            if (item != 0)
                return false;

        return true;
    }
}

public sealed record JpegValidationResult
{
    private JpegValidationResult(bool isValid, byte[] imageData, string errorMessage, JpegValidationErrorReason errorReason)
    {
        IsValid = isValid;
        ImageData = imageData;
        ErrorMessage = errorMessage;
        ErrorReason = errorReason;
    }

    public bool IsValid { get; }
    public byte[] ImageData { get; }
    public string ErrorMessage { get; }
    public JpegValidationErrorReason ErrorReason { get; }

    public static JpegValidationResult Valid(byte[] imageData)
    {
        ArgumentNullException.ThrowIfNull(imageData);

        return new JpegValidationResult(true, imageData, string.Empty, JpegValidationErrorReason.None);
    }

    public static JpegValidationResult Invalid(
        string errorMessage,
        JpegValidationErrorReason errorReason = JpegValidationErrorReason.InvalidJpeg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new JpegValidationResult(false, [], errorMessage, errorReason);
    }
}

public enum JpegValidationErrorReason
{
    None = 0,
    InvalidJpeg = 1,
    MaxLengthExceeded = 2,
    InvalidMaxLength = 3
}
