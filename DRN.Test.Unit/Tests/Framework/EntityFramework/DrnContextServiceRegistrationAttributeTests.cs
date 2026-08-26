using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel.Domain;

namespace DRN.Test.Unit.Tests.Framework.EntityFramework;

public class DrnContextServiceRegistrationAttributeTests
{
    [Fact]
    public void GetEntityTypeValidationResult_Should_Scope_Duplicates_By_AppId()
    {
        Type[] domainTypes = [typeof(FirstPartitionEntity), typeof(SecondPartitionEntity)];

        var result = DrnContextServiceRegistrationAttribute.GetEntityTypeValidationResult(domainTypes);

        result.MissingEntityTypes.Should().BeEmpty();
        result.DuplicateEntityTypes.Should().BeEmpty();
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

[EntityType<SecondValidationApp>(250)]
public sealed class SecondPartitionEntity : SourceKnownEntity;
