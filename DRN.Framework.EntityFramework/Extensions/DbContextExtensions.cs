using DRN.Framework.EntityFramework.Context;
using DRN.Framework.EntityFramework.ValueGenerator;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DRN.Framework.EntityFramework.Extensions;

internal static class DbContextExtensions
{
    public static void ModelCreatingDefaults(this DbContext dbContext, ModelBuilder modelBuilder)
    {
        var context = dbContext.GetType();
        modelBuilder
            .HasDefaultSchema(context.Name.ToSnakeCase())
            .Ignore<DomainEvent>()
            .ApplyConfigurationsFromAssembly(context.Assembly,
                configuration => configuration.Namespace!.Contains(context.Namespace!));
        
        
        var baseEntityType = typeof(Entity);
        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(e => baseEntityType.IsAssignableFrom(e.ClrType) && 
                        e.ClrType != baseEntityType); // Exclude base class itself

        foreach (var entityType in entityTypes)
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
        
            // Auto-configure common properties if they exist
            if (entityType.ClrType.GetProperty(nameof(Entity.ExtendedProperties)) != null)
            {
                entity.Property(nameof(Entity.ExtendedProperties))
                    .HasDefaultValueSql("{}")
                    .ValueGeneratedOnAdd();
                entity.Property(nameof(Entity.Id)).HasValueGenerator<LongIdValueGenerator>();
            }

            // if (entityType.ClrType.GetProperty("IsDeleted") != null)
            // {
            //     entity.HasQueryFilter(e => !((BaseEntity)e).IsDeleted);
            //     entity.HasQueryFilter(e => EF.Property<bool>(e, "IsDeleted") == false);
            //     entity.Property("IsDeleted").HasDefaultValue(false);
            // }
        }
    }

    public static void MarkEntities(this DbContext dbContext)
    {
        var entries = dbContext.ChangeTracker
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

    public static TContext CreateDbContext<TContext>(this string[] args) where TContext : DbContext
    {
        var connectionString = args.FirstOrDefault()!;
        var optionsBuilder = string.IsNullOrWhiteSpace(connectionString)
            ? DbContextConventions.UpdateDbContextOptionsBuilder<TContext>()
            : DbContextConventions.UpdateDbContextOptionsBuilder<TContext>(
                new NpgsqlDataSourceBuilder(connectionString).Build());

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }
}