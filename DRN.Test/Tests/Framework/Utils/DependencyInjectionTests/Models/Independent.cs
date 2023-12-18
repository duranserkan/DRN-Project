namespace DRN.Test.Tests.Framework.Utils.DependencyInjectionTests.Models;

public interface IIndependent
{
}

[Transient<IIndependent>]
public class Independent : IIndependent
{
}