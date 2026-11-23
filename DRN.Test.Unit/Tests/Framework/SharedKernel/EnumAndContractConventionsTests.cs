using System.Text.Json;
using System.Text.Json.Serialization;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Json;
using Sample.Contract.QA.Categories;
using Sample.Contract.QA.Tags;
using Sample.Domain;
using Sample.Domain.QA.Tag;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel;

public class EnumAndContractConventionsTests
{
    [Fact]
    public void CategoryResponse_Should_Not_Inherit_Dto_And_CategoryPostRequest_Should_Reject_Unknown_Fields()
    {
        typeof(Dto).IsAssignableFrom(typeof(CategoryResponse)).Should().BeFalse();
        typeof(Dto).IsAssignableFrom(typeof(TagResponse)).Should().BeFalse();

        var valid = JsonSerializer.Deserialize<CategoryPostRequest>("""{"name":"Valid"}""", JsonConventions.DefaultOptions);
        valid.Should().NotBeNull();
        valid!.Name.Should().Be("Valid");

        var rejectUnknown = () => JsonSerializer.Deserialize<CategoryPostRequest>(
            """{"name":"Valid","isApproved":true}""", JsonConventions.DefaultOptions);
        rejectUnknown.Should().Throw<JsonException>();

        var responseJson = JsonSerializer.Serialize(new CategoryResponse
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UnixEpoch,
            ModifiedAt = DateTimeOffset.UnixEpoch,
            Name = "Public"
        }, JsonConventions.DefaultOptions);
        using var doc = JsonDocument.Parse(responseJson);
        doc.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(new[] { "id", "createdAt", "modifiedAt", "name" });
    }

    [Fact]
    public void TagType_Should_Be_Single_Shared_Symbol_And_SampleEntityTypes_Should_Carry_DomainOnly()
    {
        var modelType = typeof(TagEntity).GetProperty(nameof(TagEntity.Model))!.PropertyType;
        var domainEnumType = modelType.GetProperty(nameof(TagValueModel.Type))!.PropertyType;
        domainEnumType.Should().Be(typeof(TagType));
        Attribute.IsDefined(typeof(TagType), typeof(AllowZeroAttribute), inherit: false).Should().BeTrue();
        Attribute.IsDefined(typeof(TagType), typeof(DomainOnlyAttribute), inherit: false).Should().BeFalse();

        Attribute.IsDefined(typeof(SampleEntityTypes), typeof(DomainOnlyAttribute), inherit: false).Should().BeTrue();
        var attr = typeof(TagEntity).GetCustomAttributes(typeof(SampleEntityTypeAttribute), inherit: false)
            .Should().ContainSingle().Which.Should().BeOfType<SampleEntityTypeAttribute>().Subject;
        attr.EntityType.Should().Be((byte)SampleEntityTypes.Tag);
    }

    [Flags]
    public enum SampleFlags
    {
        Read = 1,
        Write = 2,
        [JsonStringEnumMemberName("exec-op")]
        Execute = 4
    }

    [Fact]
    public void JsonConventions_Should_Use_Native_JsonStringEnumConverter_Including_Flags()
    {
        JsonConventions.DefaultOptions.Converters.Should().ContainSingle(converter => converter.GetType() == typeof(JsonStringEnumConverter));

        var combined = SampleFlags.Read | SampleFlags.Write;
        var combinedJson = JsonSerializer.Serialize(combined, JsonConventions.DefaultOptions);
        combinedJson.Should().Be("\"Read, Write\"");
        JsonSerializer.Deserialize<SampleFlags>(combinedJson, JsonConventions.DefaultOptions).Should().Be(combined);

        var customNamed = SampleFlags.Read | SampleFlags.Execute;
        var customNamedJson = JsonSerializer.Serialize(customNamed, JsonConventions.DefaultOptions);
        customNamedJson.Should().Be("\"Read, exec-op\"");
        JsonSerializer.Deserialize<SampleFlags>(customNamedJson, JsonConventions.DefaultOptions).Should().Be(customNamed);

        var dictionaryJson = JsonSerializer.Serialize(new Dictionary<SampleFlags, int> { [combined] = 1 }, JsonConventions.DefaultOptions);
        dictionaryJson.Should().Be("""{"Read, Write":1}""");
        JsonSerializer.Deserialize<Dictionary<SampleFlags, int>>(dictionaryJson, JsonConventions.DefaultOptions)!
            .Should().Contain(combined, 1);

        var unknownName = () => JsonSerializer.Deserialize<SampleFlags>("\"UnknownFlag\"", JsonConventions.DefaultOptions);
        unknownName.Should().Throw<JsonException>();
    }

    [Fact]
    public void TagPostRequest_Should_Track_TagType_Presence_And_Reject_Unknown_Fields()
    {
        var omitted = JsonSerializer.Deserialize<TagPostRequest>("""{"name":"Missing type","model":{"stringValue":"abc"}}""", JsonConventions.DefaultOptions);
        omitted.Should().NotBeNull();
        omitted!.Model.Type.Should().BeNull();

        var explicitSystem = JsonSerializer.Deserialize<TagPostRequest>("""{"name":"System tag","model":{"type":"System"}}""", JsonConventions.DefaultOptions);
        explicitSystem!.Model.Type.Should().Be(TagType.System);

        var explicitUser = JsonSerializer.Deserialize<TagPostRequest>("""{"name":"User tag","model":{"type":"User"}}""", JsonConventions.DefaultOptions);
        explicitUser!.Model.Type.Should().Be(TagType.User);

        var unknownField = () => JsonSerializer.Deserialize<TagPostRequest>("""{"name":"Bad","model":{"type":"User","isSystem":true}}""", JsonConventions.DefaultOptions);
        unknownField.Should().Throw<JsonException>();

        var rootUnknownField = () => JsonSerializer.Deserialize<TagPostRequest>("""{"name":"Bad","isSystem":true,"model":{"type":"User"}}""", JsonConventions.DefaultOptions);
        rootUnknownField.Should().Throw<JsonException>();
    }
}
