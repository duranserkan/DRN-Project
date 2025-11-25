namespace DRN.Test.Integration.Tests.Framework.Utils.DependencyInjectionTests.Models;

public interface IIndependent
{
}

[Transient<IIndependent>]
public class Independent : IIndependent
{
}