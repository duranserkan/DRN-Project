namespace DRN.Test.Tests.Utils.DependencyInjectionTests.Models;

public interface IMultiple
{
}

[Transient<IMultiple>(tryAdd: false)]
public class Multiple : IMultiple
{
}

[Transient<IMultiple>(tryAdd: false)]
public class Multiple2 : IMultiple
{
}