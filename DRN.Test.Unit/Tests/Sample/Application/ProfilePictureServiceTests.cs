using Sample.Application.Services;
using Sample.Domain.Identity.ProfilePictures;
using Sample.Domain.Users;

namespace DRN.Test.Unit.Tests.Sample.Application;

public class ProfilePictureServiceTests
{
    [Fact]
    public async Task CreateProfilePictureAsync_Should_Store_Valid_Jpeg()
    {
        var repository = Substitute.For<IProfilePictureRepository>();
        repository.UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>()).Returns(Task.CompletedTask);
        var service = new ProfilePictureService(repository);
        var user = new SampleUser { Id = Guid.NewGuid().ToString("N") };
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "100.jpeg"));
        using var stream = new MemoryStream(jpeg);

        await service.CreateProfilePictureAsync(user, stream, jpeg.Length);

        await repository.Received(1).UpdateProfilePictureAsync(
            Arg.Is<ProfilePicture>(picture => picture.UserId == user.Id && picture.ImageData.SequenceEqual(jpeg)),
            user);
    }

    [Fact]
    public async Task CreateProfilePictureAsync_Should_Reject_NonJpeg_Payload()
    {
        var repository = Substitute.For<IProfilePictureRepository>();
        repository.UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>()).Returns(Task.CompletedTask);
        var service = new ProfilePictureService(repository);
        var user = new SampleUser { Id = Guid.NewGuid().ToString("N") };
        var pngPayload = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00 };
        using var stream = new MemoryStream(pngPayload);

        var upload = async () => await service.CreateProfilePictureAsync(user, stream, pngPayload.Length);

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

    [Fact]
    public async Task CreateProfilePictureAsync_Should_Reject_Truncated_Jpeg_Payload()
    {
        var repository = Substitute.For<IProfilePictureRepository>();
        repository.UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>()).Returns(Task.CompletedTask);
        var service = new ProfilePictureService(repository);
        var user = new SampleUser { Id = Guid.NewGuid().ToString("N") };
        var truncatedJpeg = new byte[] { 0xFF, 0xD8, 0xFF };
        using var stream = new MemoryStream(truncatedJpeg);

        var upload = async () => await service.CreateProfilePictureAsync(user, stream, truncatedJpeg.Length);

        await upload.Should().ThrowExactlyAsync<ValidationException>()
            .WithMessage("Profile picture must be a valid JPEG image.");
        await repository.DidNotReceive().UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>());
    }

    [Fact]
    public async Task CreateProfilePictureAsync_Should_Reject_MarkerOnly_Jpeg_Payload()
    {
        var repository = Substitute.For<IProfilePictureRepository>();
        repository.UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>()).Returns(Task.CompletedTask);
        var service = new ProfilePictureService(repository);
        var user = new SampleUser { Id = Guid.NewGuid().ToString("N") };
        var markerOnlyJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9 };
        using var stream = new MemoryStream(markerOnlyJpeg);

        var upload = async () => await service.CreateProfilePictureAsync(user, stream, markerOnlyJpeg.Length);

        await upload.Should().ThrowExactlyAsync<ValidationException>()
            .WithMessage("Profile picture must be a valid JPEG image.");
        await repository.DidNotReceive().UpdateProfilePictureAsync(Arg.Any<ProfilePicture>(), Arg.Any<SampleUser>());
    }
}
