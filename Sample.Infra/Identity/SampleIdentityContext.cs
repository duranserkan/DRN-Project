using DRN.Framework.EntityFramework.Context.Identity;
using Sample.Domain.Identity.ProfilePictures;
using Sample.Domain.Users;

namespace Sample.Infra.Identity;

public class SampleIdentityContext : DrnContextIdentity<SampleIdentityContext, SampleUser>
{
    public SampleIdentityContext(DbContextOptions<SampleIdentityContext> options) : base(options)
    {
    }

    public SampleIdentityContext() : base(null)
    {
    }

    public DbSet<ProfilePicture> ProfilePictures { get; set; }
}