using DRN.Framework.Utils.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.EntityFramework.Context;

[HasDRNContextServiceCollectionModule]
public class DrnContext<TContext> : DbContext where TContext : DbContext
{
    protected DrnContext(DbContextOptions<TContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var context = GetType();
        modelBuilder.ApplyConfigurationsFromAssembly(context.Assembly, configuration => configuration.Namespace!.Contains(context.Namespace!));
    }
}

public class HasDRNContextServiceCollectionModuleAttribute : HasServiceCollectionModuleAttribute
{
    static HasDRNContextServiceCollectionModuleAttribute()
    {
        ModuleMethodInfo = typeof(ServiceCollectionExtensions).GetMethod(nameof(ServiceCollectionExtensions.AddDbContextsWithConventions))!;
    }
}