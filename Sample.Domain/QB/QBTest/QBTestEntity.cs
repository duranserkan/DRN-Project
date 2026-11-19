using System.ComponentModel.DataAnnotations;
using Sample.Contract.QB.QBTests;

namespace Sample.Domain.QB.QBTest;

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

    public QBTestDto ToDto() => new(this)
    {
        TestValue = TestValue,
        TestValueString = TestValueString
    };
}
