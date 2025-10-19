using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Logging;

namespace DRN.Framework.Utils.Models;

[Singleton<DevelopmentStatus>]
public class DevelopmentStatus
{
    private readonly List<DbContextChangeModel> _contextModels = [];
    public IReadOnlyList<DbContextChangeModel> Models => _contextModels;
    internal void AddChangeModel(DbContextChangeModel contextModel) => _contextModels.Add(contextModel);
    public bool HasPendingChanges => _contextModels.Any(c => c.Flags.HasPendingModelChanges);
}

public record DbContextChangeModelFlags(bool HasPendingModelChanges, bool Prototype, bool UsePrototypeMode, bool UsePrototypeModeWhenMigrationExists)
{
    public bool HasPendingMigrations { get; private set; }
    public bool HasPendingChanges { get; internal set; }
    public bool HasPendingModelChangesForPrototype { get; internal set; }
    public bool HasPendingMigrationsWithoutPendingModelChanges { get; internal set; }
    public bool Migrate { get; internal set; }

    public void SetMigrationFlags(bool migrate, int migrationCount, int pendingMigrationCount)
    {
        Migrate = migrate;
        HasPendingMigrations = pendingMigrationCount > 0;
        HasPendingChanges = HasPendingMigrations || HasPendingModelChanges;
        HasPendingModelChangesForPrototype = (migrationCount == 0 && HasPendingModelChanges) ||
                                             (migrationCount > 0 && UsePrototypeModeWhenMigrationExists && HasPendingModelChanges);

        HasPendingMigrationsWithoutPendingModelChanges = pendingMigrationCount > 0 && !HasPendingModelChanges;
    }
}

public class DbContextChangeModel
{
    public DbContextChangeModel(string name, bool migrate, IReadOnlyList<string> migrations, IReadOnlyList<string> appliedMigrations, DbContextChangeModelFlags flags)
    {
        Name = name;
        Migrations = migrations;
        AppliedMigrations = appliedMigrations;
        PendingMigrations = migrations.Except(appliedMigrations).ToArray();
        LastAppliedMigration = appliedMigrations.LastOrDefault() ?? "n/a";
        LastPendingMigration = PendingMigrations.LastOrDefault() ?? "n/a";

        Flags = flags;
        Flags.SetMigrationFlags(migrate, appliedMigrations.Count, PendingMigrations.Count);
    }

    public string Name { get; }
    public IReadOnlyList<string> Migrations { get; }
    public IReadOnlyList<string> AppliedMigrations { get; }
    public DbContextChangeModelFlags Flags { get; }
    public IReadOnlyList<string> PendingMigrations { get; }
    public string LastAppliedMigration { get; }
    public string LastPendingMigration { get; }

    public void LogChanges(IScopedLog? scopedLog, string environment)
    {
        if (scopedLog == null) return;

        scopedLog.AddToActions($"{Name} has {Migrations.Count} migrations");
        scopedLog.AddToActions($"{Name} has {AppliedMigrations.Count} applied migrations. Last applied: {LastAppliedMigration}");
        scopedLog.AddToActions($"{Name} has {PendingMigrations.Count} pending migrations. Last pending: {LastPendingMigration}");
        scopedLog.AddToActions(Flags.HasPendingModelChanges ? $"{Name} has pending model changes" : $"{Name} has no pending model changes");
        if (Flags is { HasPendingChanges: false, Prototype: true })
            scopedLog.AddToActions($"existing {Name} db is used for prototyping mode since there is no pending changes");
        if (!Flags.Migrate)
            scopedLog.AddToActions($"{Name} auto migration disabled in {environment}");
    }
}