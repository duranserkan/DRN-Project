using DRN.Framework.EntityFramework.Context.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DRN.Nexus.Infra.Identity;

//https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization
public class NexusIdentityContext : DrnContextIdentity<NexusIdentityContext, IdentityUser>
{
    public NexusIdentityContext(DbContextOptions<NexusIdentityContext> options) : base(options)
    {
    }

    public NexusIdentityContext() : base(null)
    {
    }
}