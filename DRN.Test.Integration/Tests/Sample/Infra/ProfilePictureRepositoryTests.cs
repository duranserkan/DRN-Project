using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sample.Domain.Identity;
using Sample.Domain.Identity.ProfilePictures;
using Sample.Domain.Users;
using Sample.Infra;
using Sample.Infra.Identity;

namespace DRN.Test.Integration.Tests.Sample.Infra;

public class ProfilePictureRepositoryTests
{
    [Theory]
    [DataInline]
    public async Task UpdateProfilePictureAsync_Should_Throw_ValidationException_And_Rollback_When_Claim_Update_Fails(DrnTestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        var mockClaimRepo = Substitute.For<IUserClaimRepository>();
        mockClaimRepo.UpdateProfilePictureVersionClaimAsync(Arg.Any<SampleUser>(), Arg.Any<byte>())
            .Returns(IdentityResult.Failed(new IdentityError
            {
                Code = "ClaimUpdateError",
                Description = "Custom claim update failed"
            }));
        context.ServiceCollection.AddScoped(_ => mockClaimRepo);

        await context.ContainerContext.Postgres.ApplyMigrationsAsync();

        var identityContext = context.GetRequiredService<SampleIdentityContext>();
        var user = new SampleUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = "testuser@example.com",
            Email = "testuser@example.com"
        };
        identityContext.Users.Add(user);
        await identityContext.SaveChangesAsync();

        var repository = context.GetRequiredService<IProfilePictureRepository>();
        var picture = new ProfilePicture(user, [0xFF, 0xD8, 0xFF, 0xD9]);

        var act = async () => await repository.UpdateProfilePictureAsync(picture, user);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.WithMessage("*Custom claim update failed*");

        identityContext.ChangeTracker.Clear();
        var savedPicture = await identityContext.ProfilePictures.FirstOrDefaultAsync(p => p.UserId == user.Id);
        savedPicture.Should().BeNull();
    }
}
