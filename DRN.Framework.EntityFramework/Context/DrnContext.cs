using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.EntityFramework.Context;

public class DrnContext : DbContext
{
    protected DrnContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var context = GetType();
        modelBuilder.ApplyConfigurationsFromAssembly(context.Assembly, configuration => configuration.Namespace!.Contains(context.Namespace!));
    }
}