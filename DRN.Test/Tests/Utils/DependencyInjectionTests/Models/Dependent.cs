

namespace DRN.Test.Tests.Utils.DependencyInjectionTests.Models;

[Scoped<Dependent>]
public class Dependent
{
    public IIndependent Independent { get; }


    public Dependent(IIndependent independent)
    {
        Independent = independent;
    }
}