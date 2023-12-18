namespace DRN.Test.Tests.Framework.Utils.DependencyInjectionTests.Models;

public interface IMultiple
{
}

[Transient<IMultiple>(tryAdd: false)]
public class Multiple : IMultiple
{
    public IMultipleIndependent Independent { get; }

    public Multiple(IMultipleIndependent independent)
    {
        Independent = independent;
    }
}

[Transient<IMultiple>(tryAdd: false)]
public class Multiple2 : IMultiple
{
}