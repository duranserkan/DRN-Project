namespace DRN.Test.Integration.Tests.Framework.Utils.DependencyInjectionTests.Models;

public interface IMultipleIndependent
{
}

[Transient<IMultipleIndependent>]
public class MultipleIndependent : IMultipleIndependent
{
}