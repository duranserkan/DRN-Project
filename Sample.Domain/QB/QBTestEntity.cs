using System.ComponentModel.DataAnnotations;

namespace Sample.Domain.QB;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class QBEntityTypeAttribute(QBEntityTypes entityType)
    : EntityTypeAttribute<TestApp>((byte)entityType);

public enum QBEntityTypes : byte
{
    QBTestEntity = 255
}

[QBEntityType(QBEntityTypes.QBTestEntity)]
public class QBTestEntity : AggregateRoot
{
    public long TestValue { get; init; }

    [MaxLength(100)]
    public string TestValueString { get; init; } = string.Empty;
}
