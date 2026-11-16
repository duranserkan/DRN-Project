using DRN.Framework.EntityFramework.Context.Identity;
using Sample.Domain.SampleIdentity.ProfilePictures;
using SampleUser = Sample.Domain.SampleIdentity.SampleUser;

namespace Sample.Infra.SampleIdentity;

[SampleIdentityContextNpgsqlDbContextOptions]
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
