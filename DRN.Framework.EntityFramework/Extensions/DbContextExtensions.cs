using DRN.Framework.EntityFramework.Context;
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