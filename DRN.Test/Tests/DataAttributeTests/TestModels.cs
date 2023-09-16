namespace DRN.Test.Tests.DataAttributeTests;

public interface IMockable
{
    public int Max { get; }
}

public class ToBeRemovedService : IMockable
{
    public int Max { get; set; }
}

public class ReplacingService : IMockable
{
    public int Max => int.MaxValue;
}

public class ComplexInline
{
    public ComplexInline(int count)
    {
        Count = count;
    }

    public int Count { get; }
}