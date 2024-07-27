using DRN.Framework.EntityFramework.Attributes;
using DRN.Framework.EntityFramework.Extensions;
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
///             <description>Context needs to be created in design time as a factory for migrations <see cref="IDesignTimeDbContextFactory{TContext}"/></description>
///         </item>
///     </list>
/// <br/>
/// <a href="https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model">Identity model customization docs</a>
/// </summary>
///<example>
/// <b>EF Tool Usage</b>
///<code>
/// dotnet tool install --global dotnet-ef
/// dotnet tool update
///</code>
/// </example>
///<example>
///<b>From Project Root to add new migration</b>
///<code>
/// dotnet ef migrations add --context [ContextName] [MigrationName]
///</code>
///<b>From Project Root to update database(can be used to revert applied migrations)</b>
///<code>
/// dotnet ef database update --context [ContextName] [MigrationName] -- "connectionString"
///</code>
///<b>From Project Root to list migration and changes</b>
///<code>
/// dotnet ef migrations list --context [ContextName]
/// dotnet ef migrations has-pending-model-changes --context [ContextName]
/// dotnet ef migrations script --context [ContextName]
///</code>
///<b>From Project Root to remove unapplied migrations</b>
///<code>
/// dotnet ef migrations remove --context [ContextName]  -- "connectionString"
///</code>
/// </example>
[DrnContextServiceRegistration, DrnContextDefaults, DrnContextPerformanceDefaults]
public abstract class DrnContext<TContext> : DbContext, IDesignTimeDbContextFactory<TContext>, IDesignTimeServices where TContext : DrnContext<TContext>, new()
{
    /// Initializes a new instance of the <see cref="DrnContext"/> class.
    protected DrnContext(DbContextOptions<TContext>? options) : base(options ?? new DbContextOptions<TContext>())
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        this.ModelCreatingDefaults(modelBuilder);
    }

    public TContext CreateDbContext(string[] args) => args.CreateDbContext<TContext>();

    public void ConfigureDesignTimeServices(IServiceCollection serviceCollection) =>
        serviceCollection.AddSingleton<IMigrationsScaffolder, DrnMigrationsScaffolder>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.MarkEntities();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new())
    {
        this.MarkEntities();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}