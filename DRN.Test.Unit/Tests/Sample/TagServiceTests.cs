using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Scope;
using Sample.Application.Services.QA.Tags;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tag;
using Tag = Sample.Domain.QA.Tag.TagEntity;

namespace DRN.Test.Unit.Tests.Sample;

public class TagServiceTests
{
    [Fact]
    public async Task CreateResponseAsync_And_CreateResponsesAsync_Should_Map_Tag_Responses_And_Guard_Nulls()
    {
        var repository = Substitute.For<ITagRepository>();
        var service = new TagService(repository);
        var id = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var date = new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero);
        var tag = new Tag("Mapped tag")
        {
            EntityIdSource = new SourceKnownEntityId(new SourceKnownId(14, createdAt, 0, 0, 0), id, 6, true, false),
            ModifiedAt = createdAt.AddMinutes(2),
            Model = new TagValueModel
            {
                BoolValue = true,
                StringValue = "value",
                Max = 100,
                Min = 10,
                Other = 42,
                Date = date,
                Type = TagType.User
            }
        };

        var response = await service.CreateResponseAsync(tag);
        response.Id.Should().Be(id);
        response.CreatedAt.Should().Be(createdAt);
        response.ModifiedAt.Should().Be(createdAt.AddMinutes(2));
        response.Name.Should().Be("Mapped tag");
        response.Model.BoolValue.Should().BeTrue();
        response.Model.StringValue.Should().Be("value");
        response.Model.Max.Should().Be(100);
        response.Model.Min.Should().Be(10);
        response.Model.Other.Should().Be(42);
        response.Model.Date.Should().Be(date);
        response.Model.Type.Should().Be(TagType.User);

        var batch = await service.CreateResponsesAsync([tag]);
        batch.Should().ContainSingle().Which.Should().BeEquivalentTo(response);

        Func<Task> nullEntity = () => service.CreateResponseAsync(null!);
        Func<Task> nullModel = () => service.CreateResponseAsync(new Tag("Missing model"));
        await nullEntity.Should().ThrowAsync<ArgumentNullException>();
        await nullModel.Should().ThrowAsync<ArgumentNullException>();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Func<Task> canceled = () => service.CreateResponseAsync(tag, cancellation.Token);
        await canceled.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CreateEntityAsync_Should_Map_User_Tag_Validate_Type_And_Honor_Cancellation()
    {
        var repository = Substitute.For<ITagRepository>();
        var service = new TagService(repository);
        var date = new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero);
        var request = new TagPostRequest
        {
            Name = "Created tag",
            Model = new TagModelPostRequest
            {
                BoolValue = true,
                StringValue = "value",
                Max = 100,
                Min = 10,
                Other = 42,
                Date = date,
                Type = TagType.User
            }
        };

        var created = await service.CreateEntityAsync(request);
        created.Name.Should().Be("Created tag");
        created.Model.BoolValue.Should().BeTrue();
        created.Model.StringValue.Should().Be("value");
        created.Model.Max.Should().Be(100);
        created.Model.Min.Should().Be(10);
        created.Model.Other.Should().Be(42);
        created.Model.Date.Should().Be(date);
        created.Model.Type.Should().Be(TagType.User);

        Func<Task> nullRequest = () => service.CreateEntityAsync(null!);
        Func<Task> nullRequestModel = () => service.CreateEntityAsync(new TagPostRequest { Name = "Missing model", Model = null! });
        await nullRequest.Should().ThrowAsync<ArgumentNullException>();
        ScopeContext.Problem.Detail = null;
        await nullRequestModel.Should().ThrowAsync<ValidationException>()
            .WithMessage("Tag model is required.");
        ScopeContext.Problem.Detail.Should().Be("Tag model is required.");

        TagType?[] invalidTypes = [null, TagType.System, (TagType)99];
        foreach (var invalidType in invalidTypes)
        {
            var invalidRequest = new TagPostRequest
            {
                Name = "Invalid tag",
                Model = new TagModelPostRequest { Type = invalidType }
            };
            ScopeContext.Problem.Detail = null;
            Func<Task> createInvalid = () => service.CreateEntityAsync(invalidRequest);
            await createInvalid.Should().ThrowAsync<ValidationException>()
                .WithMessage("Only User tag type is permitted.");
            ScopeContext.Problem.Detail.Should().Be("Only User tag type is permitted.");
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Func<Task> canceled = () => service.CreateEntityAsync(request, cancellation.Token);
        await canceled.Should().ThrowAsync<OperationCanceledException>();
        repository.ReceivedCalls().Should().BeEmpty();
    }
}
