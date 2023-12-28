namespace DRN.Test.Tests.Framework.Utils.DependencyInjectionTests.Models;

[Scoped<Dependent>]
public class Dependent
{
    public IIndependent Independent { get; }


    public Dependent(IIndependent independent)
    {
        Independent = independent;
    }
}