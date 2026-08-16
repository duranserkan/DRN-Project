namespace Sample.Domain;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class SampleEntityTypeAttribute(SampleEntityTypes entityType)
    : EntityTypeAttribute<DefaultApp>((byte)entityType);

public enum SampleEntityTypes : byte
{
    Answer = 1,
    AnswerComment = 2,
    Category = 3,
    Question = 4,
    QuestionComment = 5,
    Tag = 6,
    User = 7,
    Author = 8,
    TestEntity = 255
}
