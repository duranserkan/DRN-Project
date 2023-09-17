namespace DRN.Test.Tests.Testing;

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