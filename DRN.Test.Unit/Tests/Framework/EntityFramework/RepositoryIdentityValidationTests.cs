using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Entity;
using DRN.Framework.Utils.Ids;
using DRN.Test.Unit.Tests.Framework.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Sample.Domain.QA.Tags;
using Sample.Infra.QA;
using Sample.Infra.QA.Repositories;

namespace DRN.Test.Unit.Tests.Framework.EntityFramework;

public class RepositoryIdentityValidationTests
{
    [Fact]
    public void Repository_Should_Validate_Declared_Partitions_For_Default_And_Other_Entities()
    {
        using var context = new QAContext(new DbContextOptionsBuilder<QAContext>()
            .UseNpgsql("Host=localhost;Database=test").Options);
        var ids = Substitute.For<ISourceKnownEntityIdUtils>();
        var utils = Substitute.For<IEntityUtils>();
        utils.EntityId.Returns(ids);
        var repository = new TagRepository(context, utils);
        var expected = SourceKnownEntity.GetEntityTypeId<Tag>();
        var source = new SourceKnownId(12345, DateTimeOffset.UnixEpoch, 1, expected.AppId, 1);
        var matching = new SourceKnownEntityId(source, Guid.NewGuid(), expected.EntityType, true, false);
        var wrongPartition = matching with
        {
            EntityId = Guid.NewGuid(),
            Source = source with { AppId = TestApp.AppId }
        };
        var invalid = matching with { EntityId = Guid.NewGuid(), Valid = false };
        ids.Parse(matching.EntityId).Returns(matching);
        ids.Parse(wrongPartition.EntityId).Returns(wrongPartition);
        ids.Parse(invalid.EntityId).Returns(invalid);

        repository.GetEntityId(matching.EntityId).Should().Be(matching);
        repository.GetEntityId((Guid?)matching.EntityId)!.Value.Should().Be(matching);
        var rejectPartition = () => repository.GetEntityId(wrongPartition.EntityId);
        rejectPartition.Should().Throw<ValidationException>();
        var rejectInvalid = () => repository.GetEntityId(invalid.EntityId);
        rejectInvalid.Should().Throw<ValidationException>();
        repository.GetEntityId(invalid.EntityId, validate: false).Should().Be(invalid);
        repository.GetEntityId((Guid?)null).Should().BeNull();

        // A secondary entity supplies its own partition independently of the repository's partition.
        var otherExpected = SourceKnownEntity.GetEntityTypeId<CustomTestEntity>();
        var other = matching with
        {
            EntityId = Guid.NewGuid(),
            EntityType = otherExpected.EntityType,
            Source = source with { AppId = otherExpected.AppId }
        };
        var otherWrongPartition = other with
        {
            EntityId = Guid.NewGuid(),
            Source = source
        };
        ids.Parse(other.EntityId).Returns(other);
        ids.Parse(otherWrongPartition.EntityId).Returns(otherWrongPartition);

        repository.GetEntityId<CustomTestEntity>(other.EntityId).Should().Be(other);
        repository.GetEntityId<CustomTestEntity>((Guid?)other.EntityId)!.Value.Should().Be(other);
        repository.GetEntityId<CustomTestEntity>((Guid?)null).Should().BeNull();
        var rejectOtherPartition = () => repository.GetEntityId<CustomTestEntity>(otherWrongPartition.EntityId);
        rejectOtherPartition.Should().Throw<ValidationException>();
    }
}
