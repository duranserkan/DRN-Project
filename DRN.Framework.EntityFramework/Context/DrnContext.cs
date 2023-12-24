using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.EntityFramework.Context;

[HasDrnContextServiceCollectionModule]
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
        modelBuilder.Ignore<DomainEvent>();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        MarkEntities();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new CancellationToken())
    {
        MarkEntities();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void MarkEntities()
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is Entity && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();

        foreach (var entityEntry in entries)
        {
            var entity = (Entity)entityEntry.Entity;
            switch (entityEntry.State)
            {
                case EntityState.Added:
                    entity.MarkAsCreated();
                    break;
                case EntityState.Modified:
                    entity.MarkAsModified();
                    break;
                case EntityState.Deleted:
                    entity.MarkAsDeleted();
                    break;
            }
        }
    }
}

public class HasDrnContextServiceCollectionModuleAttribute : HasServiceCollectionModuleAttribute
{
    static HasDrnContextServiceCollectionModuleAttribute()
    {
        ModuleMethodInfo = typeof(ServiceCollectionExtensions).GetMethod(nameof(ServiceCollectionExtensions.AddDbContextsWithConventions))!;
    }
}