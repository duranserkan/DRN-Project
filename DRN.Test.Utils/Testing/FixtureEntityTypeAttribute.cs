using DRN.Framework.SharedKernel.Domain;

namespace DRN.Test.Utils.Testing;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FixtureEntityTypeAttribute<TApp>(FixtureEntityTypes entityType)
    : EntityTypeAttribute<TApp>((byte)entityType) where TApp : IAppId;

public enum FixtureEntityTypes : byte
{
    SingleEntity = 1,
    CustomPartition = 7,
    Walkthrough = 10,
    DefaultPartition = 101,
    X = 200,
    Y = 201,
    InheritanceBranch = 211,
    InheritanceLeaf = 212,
    Standalone = 213,
    Routing = 249,
    PartitionValidation = 250
}
