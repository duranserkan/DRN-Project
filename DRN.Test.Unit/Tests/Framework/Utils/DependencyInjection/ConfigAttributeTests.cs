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

        var config = context.GetRequiredService<EnvironmentConfig>();
        config.Environment.Should().Be(AppEnvironment.Development);
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
        connectionConfig.Foo.Should().BeNull();
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

[Config("")]
public class EnvironmentConfig
{
    public AppEnvironment Environment { get; init; }
}

[Config("ConnectionStrings")]
public class ConnectionStringsCollectionConfig
{
    [MaxLength(3)]
    public string Foo { get; init; }

    public string? Bar { get; init; }
}

[Config("ConnectionStrings")]
public class ConnectionStringsCollectionConfigWithNonPublicValue
{
    internal string Foo { get; init; }
}

[Config("ConnectionStrings", bindNonPublicProperties: false)]
public class ConnectionStringsCollectionConfigWithNonPublicValueUnbound
{
    internal string Foo { get; init; }
}

[Config("ConnectionStrings", true)]
public class ConnectionStringsCollectionConfigWithInvalidValue
{
    [MaxLength(2)]
    public string Foo { get; init; }
}

[Config("ConnectionStrings")]
public class ConnectionStringsCollectionConfigWithMissingFooValue
{
}