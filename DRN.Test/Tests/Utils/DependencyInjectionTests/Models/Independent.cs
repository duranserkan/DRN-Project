namespace DRN.Test.Tests.Utils.DependencyInjectionTests.Models;

public interface IIndependent
{
}

[Transient<IIndependent>]
public class Independent : IIndependent
{
}