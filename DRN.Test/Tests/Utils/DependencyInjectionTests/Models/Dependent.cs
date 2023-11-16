

namespace DRN.Test.Tests.Utils.DependencyInjectionTests.Models;

[Scoped<Dependent>]
public class Dependent
{
    private readonly IIndependent _independent;

    public Dependent(IIndependent independent)
    {
        _independent = independent;
    }
}