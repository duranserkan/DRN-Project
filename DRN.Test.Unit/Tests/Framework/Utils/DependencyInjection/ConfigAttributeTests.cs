using System.ComponentModel.DataAnnotations;
using DRN.Framework.SharedKernel.Enums;
using ValidationException = DRN.Framework.SharedKernel.ValidationException;

namespace DRN.Test.Unit.Tests.Framework.Utils.DependencyInjection;

public class ConfigAttributeTests
{
    private const string InvalidConfig = nameof(ConnectionStringsCollectionConfigWithInvalidValue);
    private const string MissingFoo = nameof(ConnectionStringsCollectionConfigWithMissingFooValue);

    [Theory]
    [DataInlineUnit]
    public void EnvironmentConfig_Should_Be_Resolved(UnitTestContext context)
    {
        context.ServiceCollection.AddServicesWithAttributes();

        var config = context.GetRequiredService<QuoteConfig>();

        var duckTest = "If it looks like a duck, swims like a duck, and quacks like a duck, then it probably is a duck";
        var philosophicalRazor = "Never attribute to malice that which can be adequately explained by incompetence or stupidity";
        config.DuckTest.Should().Be(duckTest);
        config.PhilosophicalRazor.Should().Be(philosophicalRazor);

        //environment is overriden by environment variables on dev pc therefore Environment2 is checked instead
        config.Environment2.Should().Be(AppEnvironment.Staging);
    }

    [Theory]
    [DataInlineUnit]
    public void Config_Should_Be_Resolved(UnitTestContext context)
    {
        context.ServiceCollection.AddServicesWithAttributes();

        var connectionConfig = context.GetRequiredService<ConnectionStringsCollectionConfig>();
        connectionConfig.Bar.Should().BeNull();
        connectionConfig.Foo.Should().Be(nameof(ConnectionStringsCollectionConfig.Bar));
    }

    [Theory]
    [DataInlineUnit]
    public void Config_Should_Be_Resolved_With_NonPublicValue(UnitTestContext context)
    {
        context.ServiceCollection.AddServicesWithAttributes();

        var connectionConfig = context.GetRequiredService<ConnectionStringsCollectionConfigWithNonPublicValue>();
        connectionConfig.Foo.Should().Be(nameof(ConnectionStringsCollectionConfig.Bar));
    }

    [Theory]
    [DataInlineUnit]
    public void Config_Should_Be_Resolved_With_NonPublicValue_Unbound(UnitTestContext context)
    {
        context.ServiceCollection.AddServicesWithAttributes();

        var connectionConfig = context.GetRequiredService<ConnectionStringsCollectionConfigWithNonPublicValueUnbound>();
        connectionConfig.Foo.Should().Be(string.Empty);
    }

    [Theory]
    [DataInlineUnit(MissingFoo)]
    public void Config_Should_Throw_Exception_At_ServiceProvider_Validation_For_InvalidConfig(UnitTestContext context, params string[] ignoredType)
    {
        context.ServiceCollection.AddServicesWithAttributes();

        var validationAction = () => context.ValidateServices(ignore: IgnoreValidationsFor(ignoredType));

        validationAction.Should().Throw<ValidationException>();
    }

    [Theory]
    [DataInlineUnit(InvalidConfig)]
    public void Config_Should_Throw_Exception_At_ServiceProvider_Validation_For_Missing_Foo_Value(UnitTestContext context, params string[] ignoredType)
    {
        context.ServiceCollection.AddServicesWithAttributes();

        var validationAction = () => context.ValidateServices(ignore: IgnoreValidationsFor(ignoredType));

        validationAction.Should().Throw<InvalidOperationException>();
    }

    private static Func<LifetimeAttribute, bool> IgnoreValidationsFor(params string[] ignoredTypes) =>
        attribute => ignoredTypes.Contains(attribute.ImplementationType.Name);
}

[ConfigRoot]
public class QuoteConfig
{
    public string DuckTest { get; init; } = string.Empty;
    public string PhilosophicalRazor { get; init; } = string.Empty;
    public AppEnvironment Environment2 { get; init; }
}

[Config("ConnectionStrings")]
public class ConnectionStringsCollectionConfig
{
    [MaxLength(3)]
    public string Foo { get; init; } = string.Empty;

    public string? Bar { get; init; }
}

[Config("ConnectionStrings")]
public class ConnectionStringsCollectionConfigWithNonPublicValue
{
    internal string Foo { get; init; } = string.Empty;
}

[Config("ConnectionStrings", bindNonPublicProperties: false)]
public class ConnectionStringsCollectionConfigWithNonPublicValueUnbound
{
    internal string Foo { get; init; } = string.Empty;
}

[Config("ConnectionStrings", true)]
public class ConnectionStringsCollectionConfigWithInvalidValue
{
    [MaxLength(2)]
    public string Foo { get; init; } = string.Empty;
}

[Config("ConnectionStrings")]
public class ConnectionStringsCollectionConfigWithMissingFooValue
{
}