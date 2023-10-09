namespace DRN.Test.Tests.Testing;

public interface IMockable
{
    public int Max { get; }
}

public class ToBeRemovedService : IMockable
{
    public int Max { get; set; }
}

public class DependentService : IMockable
{
    private readonly IMockable _mockable;

    public DependentService(IMockable mockable)
    {
        _mockable = mockable;
    }

    public int Max => _mockable.Max;
}

public class ComplexInline
{
    public ComplexInline(int count)
    {
        Count = count;
    }

    public int Count { get; }
}