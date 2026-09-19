using System.Text.Json;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Testing.Extensions;
using static DRN.Test.Integration.Tests.Framework.Utils.SourceKnownIds.IetfTestData;

namespace DRN.Test.Integration.Tests.Framework.Utils.SourceKnownIds;

/// <summary>
/// Verifies that System.Text.Json serialization and deserialization fully support both plaintext
/// <see cref="SourceKnownEntityId"/> (RFC 9562 UUIDv8) and AES-256 encrypted Secure
/// <see cref="SourceKnownEntityId"/>, ensuring that:
/// 1. SKEID GUIDs serialize to JSON strings and deserialize with bit-for-bit identity.
/// 2. Deserialized GUIDs parse back into <see cref="SourceKnownEntityId"/> with intact metadata.
/// 3. Standard hyphenated 'D' format (lowercase, uppercase, mixed-case) in JSON is accepted, while non-standard formats ('N', 'B', 'P') are strictly rejected per System.Text.Json rules.
/// 4. <see cref="SourceKnownEntity"/> JSON serialization correctly includes external GUIDs while ignoring internal IDs.
/// 5. DTOs deriving from <see cref="Dto"/> roundtrip cleanly via JSON serialization and deserialization.
/// 6. Nullable GUID properties handle explicit null, missing properties, and populated values accurately.
/// 7. Batch collections across distinct generated IDs serialize and deserialize without data loss or corruption.
/// 8. JSON dictionaries with GUID keys serialize to string-keyed objects and deserialize accurately.
/// 9. Serialization behaves consistently across standard web options (<see cref="JsonConventions.DefaultOptions"/>, Web, General).
/// 10. Async stream serialization and deserialization preserve full fidelity as in ASP.NET Core request pipelines.
/// Uses test data from <see cref="IetfTestData"/>.
/// </summary>
public class SourceKnownEntityIdJsonCompatibilityTests
{
    [Theory]
    [DataInline]
    public async Task Json_Serialization_And_Deserialization_Should_Support_Plain_And_Secure_Skeids_With_Full_Compatibility(DrnTestContext context)
    {
        // 1. Arrange configuration and identity services
        var (entityIdUtils, plainSkeid, secureSkeid) = Setup(context);

        // 2. Direct SourceKnownEntity JSON serialization (External Guid Id exposure & internal long Id encapsulation)
        var entity = new IetfDraftTestEntity(plainSkeid.Source.Id)
        {
            PlainId = plainSkeid.EntityId,
            SecureId = secureSkeid.EntityId,
            Name = "IetfJsonEntity",
            EntityIdSource = plainSkeid
        };

        var entityJson = JsonSerializer.Serialize(entity, JsonConventions.DefaultOptions);
        using (var doc = JsonDocument.Parse(entityJson))
        {
            var root = doc.RootElement;
            root.TryGetProperty(nameof(SourceKnownEntity.Id), out var idProp).Should().BeTrue("external Guid EntityId must be serialized under property name 'Id' via [JsonPropertyName(nameof(Id))]");
            idProp.GetString().Should().Be(PlainGuid.ToString());
            idProp.GetGuid().Should().Be(PlainGuid);

            root.TryGetProperty("plainId", out var plainProp).Should().BeTrue();
            plainProp.GetString().Should().Be(PlainGuid.ToString());

            root.TryGetProperty("secureId", out var secureProp).Should().BeTrue();
            secureProp.GetString().Should().Be(SecureGuid.ToString());

            root.TryGetProperty("name", out var nameProp).Should().BeTrue();
            nameProp.GetString().Should().Be("IetfJsonEntity");

            root.TryGetProperty("createdAt", out _).Should().BeTrue();

            // Sensitive internal fields must be omitted by [JsonIgnore]
            root.TryGetProperty("entityIdSource", out _).Should().BeFalse("EntityIdSource must be JsonIgnored");
            root.TryGetProperty("isPendingInsert", out _).Should().BeFalse("IsPendingInsert must be JsonIgnored");
            root.TryGetProperty("domainEvents", out _).Should().BeFalse("DomainEvents must not be serialized");
        }

        // 3. DTO round-trip serialization and deserialization with SKEID validation
        var plainDto = new IetfDraftTestDto(entity)
        {
            PlainId = plainSkeid.EntityId,
            SecureId = secureSkeid.EntityId,
            Name = entity.Name
        };

        plainDto.ValidateObjectSerialization();

        var plainDtoJson = JsonSerializer.Serialize(plainDto, JsonConventions.DefaultOptions);
        var deserializedPlainDto = JsonSerializer.Deserialize<IetfDraftTestDto>(plainDtoJson, JsonConventions.DefaultOptions);
        deserializedPlainDto.Should().NotBeNull();

        // Assert bit-for-bit Guid identity
        deserializedPlainDto.Id.Should().Be(plainSkeid.EntityId);
        deserializedPlainDto.PlainId.Should().Be(plainSkeid.EntityId);
        deserializedPlainDto.SecureId.Should().Be(secureSkeid.EntityId);
        deserializedPlainDto.Name.Should().Be(entity.Name);
        deserializedPlainDto.CreatedAt.Should().Be(plainSkeid.Source.CreatedAt);

        entityIdUtils.ShouldBeEquivalentSkeid(deserializedPlainDto.PlainId, plainSkeid, SourceKnownEntityIdFormat.Plain);
        entityIdUtils.ShouldBeEquivalentSkeid(deserializedPlainDto.SecureId, secureSkeid, SourceKnownEntityIdFormat.Secure);

        // 4. Secure SKEID DTO round-trip
        var secureEntity = new IetfDraftTestEntity(secureSkeid.Source.Id)
        {
            PlainId = plainSkeid.EntityId,
            SecureId = secureSkeid.EntityId,
            Name = "SecureEntity",
            EntityIdSource = secureSkeid
        };

        var secureDto = new IetfDraftTestDto(secureEntity)
        {
            PlainId = plainSkeid.EntityId,
            SecureId = secureSkeid.EntityId,
            Name = secureEntity.Name
        };

        secureDto.ValidateObjectSerialization();

        var secureDtoJson = JsonSerializer.Serialize(secureDto, JsonConventions.DefaultOptions);
        var deserializedSecureDto = JsonSerializer.Deserialize<IetfDraftTestDto>(secureDtoJson, JsonConventions.DefaultOptions);
        deserializedSecureDto.Should().NotBeNull();
        deserializedSecureDto.Id.Should().Be(secureSkeid.EntityId);

        entityIdUtils.ShouldBeEquivalentSkeid(deserializedSecureDto.Id, secureSkeid, SourceKnownEntityIdFormat.Secure);

        // 5. String literal UUID formats in JSON
        // System.Text.Json natively accepts standard RFC 9562/4122 36-character 'D' format (lowercase, uppercase, mixed-case)
        var supportedStandardFormats = new[]
        {
            ("Standard D (hyphenated lowercase)", plainSkeid.EntityId.ToString("D"), secureSkeid.EntityId.ToString("D")),
            ("Standard D (hyphenated uppercase)", plainSkeid.EntityId.ToString("D").ToUpperInvariant(), secureSkeid.EntityId.ToString("D").ToUpperInvariant()),
            ("Standard D (mixed case)", "00081B32-0005-8d01-8D0C-002a492c0E75", "652068A4-3612-cc4b-8ABB-83b853DC6786")
        };

        foreach (var (description, plainStr, secureStr) in supportedStandardFormats)
        {
            var json = $$"""{"plainId":"{{plainStr}}","secureId":"{{secureStr}}","description":"{{description}}"}""";
            var payload = JsonSerializer.Deserialize<FormattedGuidPayload>(json, JsonConventions.DefaultOptions);
            payload.Should().NotBeNull();
            payload.PlainId.Should().Be(plainSkeid.EntityId, $"format '{description}' must parse to bit-for-bit plain SKEID");
            payload.SecureId.Should().Be(secureSkeid.EntityId, $"format '{description}' must parse to bit-for-bit secure SKEID");

            entityIdUtils.ShouldBeEquivalentSkeid(payload.PlainId, plainSkeid, SourceKnownEntityIdFormat.Plain);
            entityIdUtils.ShouldBeEquivalentSkeid(payload.SecureId, secureSkeid, SourceKnownEntityIdFormat.Secure);
        }

        // System.Text.Json strictly enforces the standard 36-char 'D' format and rejects non-standard formats ('N', 'B', 'P')
        var rejectedFormats = new[]
        {
            ("N format (32 hex lowercase)", plainSkeid.EntityId.ToString("N")),
            ("N format (32 hex uppercase)", plainSkeid.EntityId.ToString("N").ToUpperInvariant()),
            ("B format (braced)", plainSkeid.EntityId.ToString("B")),
            ("P format (parenthesized)", plainSkeid.EntityId.ToString("P"))
        };

        foreach (var (description, invalidFormatStr) in rejectedFormats)
        {
            var invalidJson = $$"""{"plainId":"{{invalidFormatStr}}","secureId":"{{secureSkeid.EntityId:D}}","description":"{{description}}"}""";
            var act = () => JsonSerializer.Deserialize<FormattedGuidPayload>(invalidJson, JsonConventions.DefaultOptions);
            act.Should().Throw<JsonException>($"System.Text.Json requires standard 36-character 'D' format and rejects {description}");

            // Verify that .NET's Guid.Parse still recognizes alternative formats and preserves bit-for-bit SKEID identity
            var manuallyParsed = Guid.Parse(invalidFormatStr);
            manuallyParsed.Should().Be(plainSkeid.EntityId);
            entityIdUtils.ShouldBeEquivalentSkeid(manuallyParsed, plainSkeid, SourceKnownEntityIdFormat.Plain);
        }

        // 6. Nullable GUID handling
        var explicitNullJson = """{"nullablePlainId":null,"nullableSecureId":null}""";
        var explicitNullPayload = JsonSerializer.Deserialize<NullableGuidPayload>(explicitNullJson, JsonConventions.DefaultOptions);
        explicitNullPayload.Should().NotBeNull();
        explicitNullPayload.NullablePlainId.Should().BeNull();
        explicitNullPayload.NullableSecureId.Should().BeNull();

        var missingJson = """{}""";
        var missingPayload = JsonSerializer.Deserialize<NullableGuidPayload>(missingJson, JsonConventions.DefaultOptions);
        missingPayload.Should().NotBeNull();
        missingPayload.NullablePlainId.Should().BeNull();
        missingPayload.NullableSecureId.Should().BeNull();

        var populatedNullJson = $$"""{"nullablePlainId":"{{plainSkeid.EntityId:D}}","nullableSecureId":"{{secureSkeid.EntityId:D}}"}""";
        var populatedNullPayload = JsonSerializer.Deserialize<NullableGuidPayload>(populatedNullJson, JsonConventions.DefaultOptions);
        populatedNullPayload.Should().NotBeNull();
        populatedNullPayload.NullablePlainId.Should().Be(plainSkeid.EntityId);
        populatedNullPayload.NullableSecureId.Should().Be(secureSkeid.EntityId);

        entityIdUtils.Parse(explicitNullPayload.NullablePlainId, SourceKnownEntityIdFormat.Plain).Should().BeNull();
        entityIdUtils.Parse(explicitNullPayload.NullableSecureId, SourceKnownEntityIdFormat.Secure).Should().BeNull();
        entityIdUtils.ShouldBeEquivalentSkeid(populatedNullPayload.NullablePlainId!.Value, plainSkeid, SourceKnownEntityIdFormat.Plain);
        entityIdUtils.ShouldBeEquivalentSkeid(populatedNullPayload.NullableSecureId!.Value, secureSkeid, SourceKnownEntityIdFormat.Secure);

        // 7. Batch / collection serialization across 20 distinct IDs
        const int batchCount = 20;
        var batchRecords = new (long Id, SourceKnownEntityId Plain, SourceKnownEntityId Secure)[batchCount];
        for (var i = 0; i < batchCount; i++)
        {
            batchRecords[i] = (2000L + i, entityIdUtils.GeneratePlain<IetfDraftTestEntity>(), entityIdUtils.GenerateSecure<IetfDraftTestEntity>());
        }

        var batchList = batchRecords.Select(r => new FormattedGuidPayload(r.Plain.EntityId, r.Secure.EntityId, $"Batch_{r.Id}")).ToList();
        var batchJson = JsonSerializer.Serialize(batchList, JsonConventions.DefaultOptions);
        var deserializedBatch = JsonSerializer.Deserialize<List<FormattedGuidPayload>>(batchJson, JsonConventions.DefaultOptions);

        deserializedBatch.Should().NotBeNull();
        deserializedBatch.Count.Should().Be(batchCount);

        for (var i = 0; i < batchCount; i++)
        {
            var expected = batchRecords[i];
            var actual = deserializedBatch[i];

            actual.PlainId.Should().Be(expected.Plain.EntityId);
            actual.SecureId.Should().Be(expected.Secure.EntityId);
        }

        var sampleBatch = deserializedBatch[0];
        entityIdUtils.ShouldBeEquivalentSkeid(sampleBatch.PlainId, batchRecords[0].Plain, SourceKnownEntityIdFormat.Plain);
        entityIdUtils.ShouldBeEquivalentSkeid(sampleBatch.SecureId, batchRecords[0].Secure, SourceKnownEntityIdFormat.Secure);

        // 8. Dictionary JSON serialization with GUID keys
        var dict = new Dictionary<Guid, string>
        {
            [plainSkeid.EntityId] = "PlainValue",
            [secureSkeid.EntityId] = "SecureValue"
        };

        var dictJson = JsonSerializer.Serialize(dict, JsonConventions.DefaultOptions);
        var deserializedDict = JsonSerializer.Deserialize<Dictionary<Guid, string>>(dictJson, JsonConventions.DefaultOptions);

        deserializedDict.Should().NotBeNull();
        deserializedDict.Count.Should().Be(2);
        deserializedDict.ContainsKey(plainSkeid.EntityId).Should().BeTrue();
        deserializedDict.ContainsKey(secureSkeid.EntityId).Should().BeTrue();
        deserializedDict[plainSkeid.EntityId].Should().Be("PlainValue");
        deserializedDict[secureSkeid.EntityId].Should().Be("SecureValue");

        // 9. Multiple JsonSerializerOptions invariance
        var optionsList = new[]
        {
            ("JsonConventions.DefaultOptions", JsonConventions.DefaultOptions),
            ("JsonSerializerDefaults.Web", new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            ("JsonSerializerDefaults.General", new JsonSerializerOptions(JsonSerializerDefaults.General))
        };

        foreach (var (optName, opt) in optionsList)
        {
            var testPayload = new FormattedGuidPayload(plainSkeid.EntityId, secureSkeid.EntityId, optName);
            var json = JsonSerializer.Serialize(testPayload, opt);
            var roundtrip = JsonSerializer.Deserialize<FormattedGuidPayload>(json, opt);

            roundtrip.Should().NotBeNull();
            roundtrip.PlainId.Should().Be(plainSkeid.EntityId, $"options '{optName}' must preserve bit-for-bit plain GUID");
            roundtrip.SecureId.Should().Be(secureSkeid.EntityId, $"options '{optName}' must preserve bit-for-bit secure GUID");

            entityIdUtils.ShouldBeEquivalentSkeid(roundtrip.PlainId, plainSkeid, SourceKnownEntityIdFormat.Plain);
            entityIdUtils.ShouldBeEquivalentSkeid(roundtrip.SecureId, secureSkeid, SourceKnownEntityIdFormat.Secure);
        }

        // 10. Async stream serialization (HTTP pipeline simulation)
        await using var memoryStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memoryStream, plainDto, JsonConventions.DefaultOptions);
        memoryStream.Position = 0;

        var streamDeserialized = await JsonSerializer.DeserializeAsync<IetfDraftTestDto>(memoryStream, JsonConventions.DefaultOptions);
        streamDeserialized.Should().NotBeNull();
        streamDeserialized.Id.Should().Be(plainSkeid.EntityId);
        streamDeserialized.PlainId.Should().Be(plainSkeid.EntityId);
        streamDeserialized.SecureId.Should().Be(secureSkeid.EntityId);

        entityIdUtils.ShouldBeEquivalentSkeid(streamDeserialized.Id, plainSkeid, SourceKnownEntityIdFormat.Plain);
        entityIdUtils.ShouldBeEquivalentSkeid(streamDeserialized.SecureId, secureSkeid, SourceKnownEntityIdFormat.Secure);
    }
}
