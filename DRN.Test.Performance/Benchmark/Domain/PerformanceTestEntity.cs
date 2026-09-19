using DRN.Framework.SharedKernel.Domain;

namespace DRN.Test.Performance.Benchmark.Domain;


public enum  PerformanceTestEntityTypes : byte
{
    TestEntity = 1,
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PerformanceTestEntityTypeAttribute(PerformanceTestEntityTypes entityType)
    : EntityTypeAttribute<TestApp>((byte)entityType);

[PerformanceTestEntityType(PerformanceTestEntityTypes.TestEntity)]
public class PerformanceTestEntity(long id) : SourceKnownEntity(id);
