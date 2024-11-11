using DRN.Framework.EntityFramework.Context.Identity;
using DRN.Nexus.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace DRN.Nexus.Infra.Identity;

//https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization
public class NexusIdentityContext : DrnContextIdentity<NexusIdentityContext, NexusUser>
{
    public NexusIdentityContext(DbContextOptions<NexusIdentityContext> options) : base(options)
    {
    }

    public NexusIdentityContext() : base(null)
    {
    }
}