using DRN.Framework.SharedKernel.Extensions;
using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.Attributes;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace Sample.Infra.DrnDataProtection;

//https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview
//https://www.nuget.org/packages/Microsoft.AspNetCore.DataProtection.EntityFrameworkCore/
[DrnDataProtectionContextOptions]
[SuppressMessage("DRN.Conventions.Architecture", "DRNCV022",
    Justification = "DrnDataProtectionContext persists ASP.NET Core Data Protection key rings only and has no Domain subdomain entities.")]
public class DrnDataProtectionContext : DrnContext<DrnDataProtectionContext>, IDataProtectionKeyContext
{
    public DrnDataProtectionContext(DbContextOptions<DrnDataProtectionContext> options) : base(options)
    {
    }

    public DrnDataProtectionContext() : base(null)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema($"__{GetType().Name}".ToSnakeCase());
    }

    [Sensitive, IgnoreLog]
    [SuppressMessage("DRN.Conventions.Persistence", "DRNCV023",
        Justification = "IDataProtectionKeyContext requires ASP.NET's DataProtectionKey storage type; this key ring is infrastructure storage, not a domain aggregate.")]
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
}
