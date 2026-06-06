using DRN.Framework.Utils.Validators;

namespace DRN.Test.Unit.Tests.Framework.Utils.Validators;

public class JpegValidatorTests
{
    [Fact]
    public async Task IsValid_Should_Accept_Valid_Jpeg()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));

        JpegValidator.IsValid(jpeg).Should().BeTrue();
    }

    [Fact]
    public async Task IsValid_Should_Accept_Valid_Jpeg_Within_MaxLength()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));

        JpegValidator.IsValid(jpeg, jpeg.Length).Should().BeTrue();
    }

    [Fact]
    public async Task IsValidAsync_Should_Accept_Valid_Jpeg_Stream()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));
        using var stream = new MemoryStream(jpeg);

        var isValid = await JpegValidator.IsValidAsync(stream, jpeg.Length);

        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_Should_Return_Valid_Result_For_Valid_Jpeg_Bytes()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));

        var result = JpegValidator.Validate(jpeg, jpeg.Length);

        result.IsValid.Should().BeTrue();
        result.ImageData.Should().Equal(jpeg);
        result.ErrorMessage.Should().BeEmpty();
        result.ErrorReason.Should().Be(JpegValidationErrorReason.None);
    }

    [Fact]
    public async Task ValidateAsync_Should_Return_Valid_Result_For_Valid_Jpeg_Stream()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));
        using var stream = new MemoryStream(jpeg);

        var result = await JpegValidator.ValidateAsync(stream, jpeg.Length);

        result.IsValid.Should().BeTrue();
        result.ImageData.Should().Equal(jpeg);
        result.ErrorMessage.Should().BeEmpty();
        result.ErrorReason.Should().Be(JpegValidationErrorReason.None);
    }

    [Fact]
    public async Task ValidateAsync_Should_Return_Invalid_Result_For_Invalid_Jpeg_Stream()
    {
        var pngPayload = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00 };
        using var stream = new MemoryStream(pngPayload);

        var result = await JpegValidator.ValidateAsync(stream, pngPayload.Length);

        result.IsValid.Should().BeFalse();
        result.ImageData.Should().BeEmpty();
        result.ErrorMessage.Should().NotBeEmpty();
        result.ErrorReason.Should().Be(JpegValidationErrorReason.InvalidJpeg);
    }

    [Fact]
    public async Task IsValidAsync_Should_Reject_Valid_Jpeg_Stream_Over_MaxLength()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));
        using var stream = new MemoryStream(jpeg);

        var isValid = await JpegValidator.IsValidAsync(stream, jpeg.Length - 1);

        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_Should_Return_Invalid_Result_For_Valid_Jpeg_Stream_Over_MaxLength()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));
        using var stream = new MemoryStream(jpeg);

        var result = await JpegValidator.ValidateAsync(stream, jpeg.Length - 1);

        result.IsValid.Should().BeFalse();
        result.ImageData.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("maximum");
        result.ErrorReason.Should().Be(JpegValidationErrorReason.MaxLengthExceeded);
    }

    [Fact]
    public async Task Validate_Should_Return_Invalid_Result_For_Valid_Jpeg_Bytes_Over_MaxLength()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));

        var result = JpegValidator.Validate(jpeg, jpeg.Length - 1);

        result.IsValid.Should().BeFalse();
        result.ImageData.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("maximum");
        result.ErrorReason.Should().Be(JpegValidationErrorReason.MaxLengthExceeded);
    }

    [Fact]
    public async Task Validate_Should_Return_Invalid_Result_For_Negative_MaxLength()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));

        var result = JpegValidator.Validate(jpeg, -1);

        result.IsValid.Should().BeFalse();
        result.ImageData.Should().BeEmpty();
        result.ErrorReason.Should().Be(JpegValidationErrorReason.InvalidMaxLength);
    }

    [Fact]
    public async Task IsValid_Should_Reject_Valid_Jpeg_Over_MaxLength()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));

        JpegValidator.IsValid(jpeg, jpeg.Length - 1).Should().BeFalse();
    }

    [Fact]
    public async Task IsValid_Should_Reject_Valid_Jpeg_When_MaxLength_Is_Negative()
    {
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));

        JpegValidator.IsValid(jpeg, -1).Should().BeFalse();
    }

    [Fact]
    public void IsValid_Should_Reject_NonJpeg_Payload()
    {
        var pngPayload = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00 };

        JpegValidator.IsValid(pngPayload).Should().BeFalse();
    }

    [Fact]
    public void IsValid_Should_Reject_Truncated_Jpeg_Payload()
    {
        var truncatedJpeg = new byte[] { 0xFF, 0xD8, 0xFF };

        JpegValidator.IsValid(truncatedJpeg).Should().BeFalse();
    }

    [Fact]
    public void IsValid_Should_Reject_MarkerOnly_Jpeg_Payload()
    {
        var markerOnlyJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9 };

        JpegValidator.IsValid(markerOnlyJpeg).Should().BeFalse();
    }
}
