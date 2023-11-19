namespace DRN.Test.Tests.Utils.DependencyInjectionTests.Models;

public interface IMultipleIndependent
{
}

[Transient<IMultipleIndependent>]
public class MultipleIndependent : IMultipleIndependent
{
}