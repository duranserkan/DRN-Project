using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel.Domain;

namespace DRN.Test.Unit.Tests.Framework.EntityFramework;

public class DrnContextServiceRegistrationAttributeTests
{
    [Fact]
    public void GetEntityTypeValidationResult_Should_Detect_Multiple_AppIds()
    {
        Type[] domainTypes = [typeof(FirstPartitionEntity), typeof(SecondPartitionEntity)];

        var result = DrnContextServiceRegistrationAttribute.GetEntityTypeValidationResult(domainTypes);

        result.MissingEntityTypes.Should().BeEmpty();
        result.DuplicateEntityTypes.Should().BeEmpty();
        result.MultipleAppIds.Should().BeEquivalentTo([121, 122]);
    }

    [Fact]
    public void GetEntityTypeValidationResult_Should_Detect_Duplicate_EntityType_Within_Same_AppId()
    {
        Type[] domainTypes = [typeof(FirstPartitionEntity), typeof(FirstPartitionDuplicateEntity)];

        var result = DrnContextServiceRegistrationAttribute.GetEntityTypeValidationResult(domainTypes);

        result.MissingEntityTypes.Should().BeEmpty();
        result.DuplicateEntityTypes.Should().HaveCount(2);
        result.MultipleAppIds.Should().BeEmpty();
    }

    [Fact]
    public void GetEntityTypeValidationResult_Should_Allow_TestApp_With_Production_App()
    {
        Type[] domainTypes = [typeof(FirstPartitionEntity), typeof(TestPartitionEntity)];

        var result = DrnContextServiceRegistrationAttribute.GetEntityTypeValidationResult(domainTypes);

        result.MissingEntityTypes.Should().BeEmpty();
        result.DuplicateEntityTypes.Should().BeEmpty();
        result.MultipleAppIds.Should().BeEmpty();
    }
}

public readonly struct FirstValidationApp : IAppId
{
    public static byte AppId => 121;
}

public readonly struct SecondValidationApp : IAppId
{
    public static byte AppId => 122;
}

[EntityType<FirstValidationApp>(250)]
public sealed class FirstPartitionEntity : SourceKnownEntity;

#pragma warning disable DRN0002 // Intentional duplicate entity type to test runtime validation
[EntityType<FirstValidationApp>(250)]
public sealed class FirstPartitionDuplicateEntity : SourceKnownEntity;
#pragma warning restore DRN0002

[EntityType<SecondValidationApp>(250)]
public sealed class SecondPartitionEntity : SourceKnownEntity;

[EntityType<TestApp>(250)]
public sealed class TestPartitionEntity : SourceKnownEntity;
