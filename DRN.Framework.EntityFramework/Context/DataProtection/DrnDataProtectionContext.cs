using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.EntityFramework.Context.DataProtection;

//https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview
//https://www.nuget.org/packages/Microsoft.AspNetCore.DataProtection.EntityFrameworkCore/
[DrnDataProtectionContextOptions]
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

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
}