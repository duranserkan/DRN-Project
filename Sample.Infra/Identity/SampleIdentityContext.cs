using DRN.Framework.EntityFramework.Context.Identity;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Identity.ProfilePictures;

namespace Sample.Infra.Identity;

public class SampleIdentityContext : DrnContextIdentity<SampleIdentityContext, IdentityUser>
{
    public SampleIdentityContext(DbContextOptions<SampleIdentityContext> options) : base(options)
    {
    }

    public SampleIdentityContext() : base(null)
    {
    }

    public DbSet<ProfilePicture> ProfilePictures { get; set; }
}