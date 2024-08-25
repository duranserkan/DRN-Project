using DRN.Framework.EntityFramework.Context.Identity;
using Microsoft.AspNetCore.Identity;

namespace Sample.Infra.Identity;

public class SampleIdentityContext : DrnContextIdentity<SampleIdentityContext, IdentityUser>
{
    public SampleIdentityContext(DbContextOptions<SampleIdentityContext> options) : base(options)
    {
    }

    public SampleIdentityContext() : base(null)
    {
    }
}