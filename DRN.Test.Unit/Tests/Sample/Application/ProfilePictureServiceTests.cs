using Sample.Application.Services;
using Sample.Domain.Identity.ProfilePictures;
using Sample.Domain.Users;

namespace DRN.Test.Unit.Tests.Sample.Application;

public class ProfilePictureServiceTests
{
    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task CreateProfilePictureAsync_Should_Store_Normalized_Jpeg(bool trailingData)
    {
        var repository = Substitute.For<IProfilePictureRepository>();
        repository.UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>()).Returns(Task.CompletedTask);
        var service = new ProfilePictureService(repository);
        var user = new SampleUser { Id = Guid.NewGuid().ToString("N") };
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));
        var payload = trailingData
            ? jpeg.Concat(new byte[] { 0x00, 0x00, 0x4D, 0x45, 0x54, 0x41 }).ToArray()
            : jpeg;
        using var stream = new MemoryStream(payload);

        await service.CreateProfilePictureAsync(user, stream, payload.Length);

        await repository.Received(1).UpdateProfilePictureAsync(
            Arg.Is<ProfilePicture>(picture => picture!.UserId == user.Id && picture.ImageData.SequenceEqual(jpeg)),
            user);
    }

    [Theory]
    [DataMemberUnit(nameof(InvalidJpegPayloads))]
    public async Task CreateProfilePictureAsync_Should_Reject_Invalid_Jpeg_Payload(byte[] payload)
    {
        var repository = Substitute.For<IProfilePictureRepository>();
        repository.UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>()).Returns(Task.CompletedTask);
        var service = new ProfilePictureService(repository);
        var user = new SampleUser { Id = Guid.NewGuid().ToString("N") };
        using var stream = new MemoryStream(payload);

        var upload = async () => await service.CreateProfilePictureAsync(user, stream, payload.Length);

        await upload.Should().ThrowExactlyAsync<ValidationException>()
            .WithMessage("Profile picture must be a valid JPEG image.");
        await repository.DidNotReceive().UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>());
    }

    [Fact]
    public async Task CreateProfilePictureAsync_Should_Reject_Payload_Over_MaxSize()
    {
        var repository = Substitute.For<IProfilePictureRepository>();
        repository.UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>()).Returns(Task.CompletedTask);
        var service = new ProfilePictureService(repository);
        var user = new SampleUser { Id = Guid.NewGuid().ToString("N") };
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));
        using var stream = new MemoryStream(jpeg);

        var upload = async () => await service.CreateProfilePictureAsync(user, stream, jpeg.Length - 1);

        await upload.Should().ThrowExactlyAsync<ValidationException>()
            .WithMessage("Profile picture exceeds the maximum allowed size.");
        await repository.DidNotReceive().UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>());
    }

    [Fact]
    public async Task CreateProfilePictureAsync_Should_Reject_Invalid_MaxSize()
    {
        var repository = Substitute.For<IProfilePictureRepository>();
        repository.UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>()).Returns(Task.CompletedTask);
        var service = new ProfilePictureService(repository);
        var user = new SampleUser { Id = Guid.NewGuid().ToString("N") };
        using var stream = new MemoryStream([]);

        var upload = async () => await service.CreateProfilePictureAsync(user, stream, -1);

        await upload.Should().ThrowExactlyAsync<ValidationException>()
            .WithMessage("Profile picture maximum size must be zero or greater.");
        await repository.DidNotReceive().UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>());
    }

    public static IEnumerable<object[]> InvalidJpegPayloads()
    {
        yield return [new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00 }];
        yield return [new byte[] { 0xFF, 0xD8, 0xFF }];
        yield return [new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9 }];
    }
}
