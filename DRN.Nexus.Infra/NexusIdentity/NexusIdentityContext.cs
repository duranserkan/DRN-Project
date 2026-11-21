using DRN.Framework.EntityFramework.Context.Identity;
using DRN.Nexus.Domain.NexusIdentity.Users;
using Microsoft.EntityFrameworkCore;

namespace DRN.Nexus.Infra.NexusIdentity;

//https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization
[NexusIdentityContextNpgsqlDbContextOptions]
public class NexusIdentityContext : DrnContextIdentity<NexusIdentityContext, NexusUser>
{
    public NexusIdentityContext(DbContextOptions<NexusIdentityContext> options) : base(options)
    {
    }

    public NexusIdentityContext() : base(null)
    {
    }

}
