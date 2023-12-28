using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.EntityFramework.Context;

/// <summary>
///     <list type="table">
///         <listheader>
///             <term>Constraints</term>
///             <description>Constructor</description>
///         </listheader>
///         <item>
///             <term>Derived class should have a constructor with options parameter</term>
///             <description>
///                 * Options will be used by attribute based dependency injection with conventions. <br/>
///                 * <b>Requires a connection string with a key named as derived class's short name by convention</b>
///             </description>
///         </item>
///         <item>
///             <term>Derived class should have a parameterless constructor</term>
///             <description>Context needs to be created in design time as a factory for migrations <see cref="IDesignTimeDbContextFactory"/></description>
///         </item>
///     </list>
/// </summary>
///<example>
/// <b>EF Tool Usage</b>
///<code>
/// dotnet tool install --global dotnet-ef
/// dotnet tool update
///</code>
/// </example>
///<example>
///<b>From Project Root</b>
///<code>
/// dotnet ef migrations add --context QAContext [MigrationName]
/// dotnet ef database update --context QAContext  -- "connectionString"
///</code>
/// </example>
[HasDrnContextServiceCollectionModule]
public abstract class DrnContext<TContext> : DbContext, IDesignTimeDbContextFactory<TContext>, IDesignTimeServices where TContext : DbContext, new()
{
    /// Initializes a new instance of the <see cref="DrnContext"/> class.
    protected DrnContext(DbContextOptions<TContext>? options) : base(options ?? new DbContextOptions<TContext>())
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var context = GetType();
        modelBuilder.ApplyConfigurationsFromAssembly(context.Assembly, configuration => configuration.Namespace!.Contains(context.Namespace!));
        modelBuilder.Ignore<DomainEvent>();
    }

    public TContext CreateDbContext(string[] args)
    {
        var contextName = typeof(TContext).Name;
        var connectionString = args.FirstOrDefault()!;
        var optionsBuilder = DbContextConventions.UpdateDbContextOptionsBuilder<TContext>(connectionString, contextName);

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }

    public void ConfigureDesignTimeServices(IServiceCollection serviceCollection) =>
        serviceCollection.AddSingleton<IMigrationsScaffolder, DrnMigrationsScaffolder>();

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

/// <summary>
/// Adds DRNContexts in derived DbContext's assembly by using <br/>
/// <see cref="ServiceCollectionExtensions.AddDbContextsWithConventions"/>
/// <br/>
/// when
/// <br/>
/// <see cref="DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes"/>
/// <br/> is called from DbContext's assembly
/// </summary>
public class HasDrnContextServiceCollectionModuleAttribute : HasServiceCollectionModuleAttribute
{
    static HasDrnContextServiceCollectionModuleAttribute()
    {
        ModuleMethodInfo = typeof(ServiceCollectionExtensions).GetMethod(nameof(ServiceCollectionExtensions.AddDbContextsWithConventions))!;
    }
}