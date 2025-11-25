namespace DRN.Test.Integration.Tests.Framework.Utils.DependencyInjectionTests.Models;

public interface IKeyed;

[ScopedWithKey<IKeyed>(1)]
public class Keyed1 : IKeyed;

[ScopedWithKey<IKeyed>(2)]
public class Keyed2 : IKeyed;

[ScopedWithKey<IKeyed>("A")]
public class KeyedA : IKeyed;

[ScopedWithKey<IKeyed>("B")]
public class KeyedB : IKeyed;

[ScopedWithKey<IKeyed>("Multiple", tryAdd: false)]
public class KeyedMultiple1 : IKeyed;

[ScopedWithKey<IKeyed>("Multiple", tryAdd: false)]
public class KeyedMultiple2 : IKeyed;

[ScopedWithKey<IKeyed>("Multiple", tryAdd: true)]
public class KeyedMultiple3 : IKeyed;

[ScopedWithKey<IKeyed>(Keyed.First)]
public class KeyedFirst : IKeyed;

[ScopedWithKey<IKeyed>(Keyed.Second)]
public class KeyedSecond(IKeyedDependency dependency) : IKeyed
{
    public IKeyedDependency Dependency { get; } = dependency;
}

public enum Keyed
{
    First = 1,
    Second
}

public interface IKeyedDependency;

[Transient<IKeyedDependency>]
public class KeyedDependency : IKeyedDependency;