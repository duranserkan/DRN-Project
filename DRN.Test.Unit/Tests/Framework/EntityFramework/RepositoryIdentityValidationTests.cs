using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Repository;
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
    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Repository_Id_Helpers_Should_Forward_Formats_Through_All_Shapes(bool configuredSecure)
    {
        using var context = new QAContext(new DbContextOptionsBuilder<QAContext>()
            .UseNpgsql("Host=localhost;Database=test").Options);
        using var settings = SettingsProvider.Development(new
        {
            NexusAppSettings = new { UseSecureSourceKnownIds = configuredSecure }
        });
        using var ids = new SourceKnownEntityIdUtils(settings, new SourceKnownIdUtils(settings));
        var utils = Substitute.For<IEntityUtils>();
        utils.EntityId.Returns(ids);
        ISourceKnownRepository<Tag> repository = new TagRepository(context, utils);
        var expected = SourceKnownEntity.GetEntityTypeId<Tag>();
        var numericId = long.MinValue | ((long)expected.AppId << 24);
        var plain = ids.GeneratePlain(numericId, expected);
        var secure = ids.GenerateSecure(numericId, expected);

        foreach (var format in new SourceKnownEntityIdFormat?[] { null, SourceKnownEntityIdFormat.Secure, SourceKnownEntityIdFormat.Plain, SourceKnownEntityIdFormat.Auto })
        {
            foreach (var original in new[] { plain, secure })
            {
                var accepted = ids.Parse(original.EntityId, format).Valid;
                var inputs = new[] { original.EntityId };
                var nullableInputs = new Guid?[] { original.EntityId };
                Func<SourceKnownEntityId>[] calls =
                [
                    () => repository.GetEntityId(original.EntityId, format: format),
                    () => repository.GetEntityId((Guid?)original.EntityId, format: format)!.Value,
                    () => repository.GetEntityId<Tag>(original.EntityId, format),
                    () => repository.GetEntityId<Tag>((Guid?)original.EntityId, format)!.Value,
                    () => repository.GetEntityIds(inputs, format: format).Single(),
                    () => repository.GetEntityIds(nullableInputs, format: format).Single()!.Value,
                    () => repository.GetEntityIds<Tag>(inputs, format).Single(),
                    () => repository.GetEntityIds<Tag>(nullableInputs, format).Single()!.Value,
                    () => repository.GetEntityIdsAsEnumerable(inputs, format: format).Single(),
                    () => repository.GetEntityIdsAsEnumerable(nullableInputs, format: format).Single()!.Value,
                    () => repository.GetEntityIdsAsEnumerable<Tag>(inputs, format).Single(),
                    () => repository.GetEntityIdsAsEnumerable<Tag>(nullableInputs, format).Single()!.Value
                ];
                foreach (var call in calls)
                {
                    if (!accepted) call.Should().Throw<ValidationException>();
                    else
                    {
                        var parsed = call();
                        parsed.Valid.Should().BeTrue();
                        parsed.Secure.Should().Be(original.Secure);
                        parsed.EntityTypeId.Should().Be(expected);
                        parsed.Source.Id.Should().Be(numericId);
                    }
                }
                repository.GetEntityId(original.EntityId, validate: false, format).Valid.Should().Be(accepted);
                repository.GetEntityIds(inputs, validate: false, format).Single().Valid.Should().Be(accepted);
                var wrongPartition = () => repository.GetEntityId<CustomTestEntity>(original.EntityId, format);
                wrongPartition.Should().Throw<ValidationException>();
            }
            repository.GetEntityId((Guid?)null, format: format).Should().BeNull();
            repository.GetEntityId<Tag>((Guid?)null, format).Should().BeNull();
            repository.GetEntityIds(new Guid?[] { null }, format: format).Should().Equal(new SourceKnownEntityId?[] { null });
            repository.GetEntityIds<Tag>(new Guid?[] { null }, format).Should().Equal(new SourceKnownEntityId?[] { null });
        }
        repository.GetEntityId((configuredSecure ? secure : plain).EntityId).Valid.Should().BeTrue();
        var otherExpected = SourceKnownEntity.GetEntityTypeId<CustomTestEntity>();
        var other = ids.GeneratePlain(long.MinValue | ((long)otherExpected.AppId << 24), otherExpected);
        repository.GetEntityIds<CustomTestEntity>(new[] { other.EntityId }, SourceKnownEntityIdFormat.Plain)
            .Single().EntityTypeId.Should().Be(otherExpected);
        var rejectOther = () => repository.GetEntityId(other.EntityId, format: SourceKnownEntityIdFormat.Plain);
        rejectOther.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Repository_Should_Reject_Undefined_Formats_For_Null_And_Empty_Inputs()
    {
        using var context = new QAContext(new DbContextOptionsBuilder<QAContext>()
            .UseNpgsql("Host=localhost;Database=test").Options);
        var utils = Substitute.For<IEntityUtils>();
        ISourceKnownRepository<Tag> repository = new TagRepository(context, utils);
        var format = (SourceKnownEntityIdFormat)3;
        Action[] calls =
        [
            () => repository.GetEntityId((Guid?)null, format: format),
            () => repository.GetEntityId<Tag>((Guid?)null, format),
            () => repository.GetEntityIds(Array.Empty<Guid>(), format: format),
            () => repository.GetEntityIds(Array.Empty<Guid?>(), format: format),
            () => repository.GetEntityIds<Tag>(Array.Empty<Guid>(), format),
            () => repository.GetEntityIds<Tag>(Array.Empty<Guid?>(), format),
            () => repository.GetEntityIdsAsEnumerable(Array.Empty<Guid>(), format: format),
            () => repository.GetEntityIdsAsEnumerable(Array.Empty<Guid?>(), format: format),
            () => repository.GetEntityIdsAsEnumerable<Tag>(Array.Empty<Guid>(), format),
            () => repository.GetEntityIdsAsEnumerable<Tag>(Array.Empty<Guid?>(), format)
        ];
        foreach (var call in calls)
            call.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("format");
    }

    [Fact]
    public void Repository_Enumerable_Should_Defer_Parsing_And_Retain_The_Format()
    {
        using var context = new QAContext(new DbContextOptionsBuilder<QAContext>()
            .UseNpgsql("Host=localhost;Database=test").Options);
        var ids = Substitute.For<ISourceKnownEntityIdUtils>();
        var utils = Substitute.For<IEntityUtils>();
        utils.EntityId.Returns(ids);
        ISourceKnownRepository<Tag> repository = new TagRepository(context, utils);
        var expected = SourceKnownEntity.GetEntityTypeId<Tag>();
        var parsed = new SourceKnownEntityId(new SourceKnownId(123, DateTimeOffset.UnixEpoch, 1, expected.AppId, 1),
            Guid.NewGuid(), expected.EntityType, true, false);
        ids.Parse(parsed.EntityId, SourceKnownEntityIdFormat.Plain).Returns(parsed);
        var inputs = new List<Guid> { parsed.EntityId };
        var deferred = repository.GetEntityIdsAsEnumerable<Tag>(inputs, SourceKnownEntityIdFormat.Plain);
        ids.DidNotReceive().Parse(Arg.Any<Guid>(), Arg.Any<SourceKnownEntityIdFormat?>());
        inputs.Add(parsed.EntityId);
        var results = deferred.ToArray();
        results.Should().Equal(parsed, parsed);
        ids.Received(2).Parse(parsed.EntityId, SourceKnownEntityIdFormat.Plain);
    }

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
