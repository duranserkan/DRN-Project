using DRN.Framework.EntityFramework.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.EntityFramework.IdentityContext;

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
[DrnContextServiceRegistration]
public abstract class DrnContextIdentity<TContext, TUser> : IdentityDbContext<TUser>, IDesignTimeDbContextFactory<TContext>, IDesignTimeServices
    where TContext : DrnContextIdentity<TContext, TUser>, new()
    where TUser : IdentityUser
{
    /// Initializes a new instance of the <see cref="DrnContextIdentity"/> class.
    protected DrnContextIdentity(DbContextOptions<TContext>? options) : base(options ?? new DbContextOptions<TContext>())
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        this.ModelCreatingDefaults(builder);
        builder.Entity<TUser>().ToTable("users");
        builder.Entity<IdentityUserToken<string>>().ToTable("user_tokens");
        builder.Entity<IdentityUserLogin<string>>().ToTable("user_logins");
        builder.Entity<IdentityUserClaim<string>>().ToTable("user_claims");
        builder.Entity<IdentityRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("user_roles");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("role_claims");
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