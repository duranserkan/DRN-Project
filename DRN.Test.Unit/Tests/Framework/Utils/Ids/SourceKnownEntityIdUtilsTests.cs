using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Data.Encryption;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class SourceKnownEntityIdUtilsTests
{
    [Theory]
    [DataInlineUnit(false, null)]
    [DataInlineUnit(true, null)]
    [DataInlineUnit(false, SourceKnownEntityIdFormat.Secure)]
    [DataInlineUnit(true, SourceKnownEntityIdFormat.Secure)]
    [DataInlineUnit(false, SourceKnownEntityIdFormat.Plain)]
    [DataInlineUnit(true, SourceKnownEntityIdFormat.Plain)]
    [DataInlineUnit(false, SourceKnownEntityIdFormat.Auto)]
    [DataInlineUnit(true, SourceKnownEntityIdFormat.Auto)]
    public void Formats_Should_Apply_To_All_Parse_And_Validate_Paths(bool configuredSecure, SourceKnownEntityIdFormat? format)
    {
        using var settings = SettingsProvider.Development<SampleApp5>(new
        {
            NexusAppSettings = new { UseSecureSourceKnownIds = configuredSecure }
        });
        using var implementation = new SourceKnownEntityIdUtils(settings, new SourceKnownIdUtils(settings));
        ISourceKnownEntityIdUtils ids = implementation;
        ISourceKnownEntityIdOperations operations = implementation;
        var expected = new EntityTypeId(200, 5);
        var numericId = long.MinValue | (5L << 24);

        foreach (var original in new[] { ids.GeneratePlain(numericId, expected), ids.GenerateSecure(numericId, expected) })
        {
            var accepted = format == SourceKnownEntityIdFormat.Auto || original.Secure == (format switch
            {
                SourceKnownEntityIdFormat.Secure => true,
                SourceKnownEntityIdFormat.Plain => false,
                _ => configuredSecure
            });
            var parsed = ids.Parse(original.EntityId, format);
            parsed.Valid.Should().Be(accepted);
            parsed.EntityId.Should().Be(original.EntityId);
            operations.Parse(original.EntityId, format).Valid.Should().Be(accepted);
            operations.Parse((Guid?)original.EntityId, format)!.Value.Valid.Should().Be(accepted);
            ids.Parse((Guid?)original.EntityId, format)!.Value.Valid.Should().Be(accepted);
            if (format == null)
            {
                implementation.Parse(original.EntityId).Valid.Should().Be(accepted);
                operations.Parse(original.EntityId).Valid.Should().Be(accepted);
                ids.Parse(original.EntityId).Valid.Should().Be(accepted);
                ids.Parse((Guid?)original.EntityId)!.Value.Valid.Should().Be(accepted);
            }

            Func<SourceKnownEntityId>[] validations =
            [
                () => ids.Validate(original.EntityId, 200, format),
                () => ids.Validate((Guid?)original.EntityId, 200, format)!.Value,
                () => ids.Validate<SampleApp5>(original.EntityId, 200, format),
                () => ids.Validate<SampleApp5>((Guid?)original.EntityId, 200, format)!.Value,
                () => ids.Validate(original.EntityId, expected, format),
                () => ids.Validate((Guid?)original.EntityId, expected, format)!.Value,
                () => ids.Validate<XEntity>(original.EntityId, format),
                () => ids.Validate<XEntity>((Guid?)original.EntityId, format)!.Value,
                () => operations.Validate(original.EntityId, 200, format),
                () => operations.Validate((Guid?)original.EntityId, 200, format)!.Value,
                () => operations.Validate<SampleApp5>(original.EntityId, 200, format),
                () => operations.Validate<SampleApp5>((Guid?)original.EntityId, 200, format)!.Value,
                () => operations.Validate(original.EntityId, expected, format),
                () => operations.Validate((Guid?)original.EntityId, expected, format)!.Value,
                () => operations.Validate<XEntity>(original.EntityId, format),
                () => operations.Validate<XEntity>((Guid?)original.EntityId, format)!.Value
            ];
            foreach (var validate in validations)
            {
                if (!accepted)
                {
                    validate.Should().Throw<ValidationException>();
                    continue;
                }

                var validated = validate();
                validated.Valid.Should().BeTrue();
                validated.Secure.Should().Be(original.Secure);
                validated.Source.Should().Be(original.Source);
                validated.EntityTypeId.Should().Be(expected);
                validated.EntityId.Should().Be(original.EntityId);
            }

            if (!accepted) continue;
            var wrongPartition = () => ids.Validate<XEntityInApp6>(original.EntityId, format);
            var wrongApp = () => ids.Validate<SampleApp6>((Guid?)original.EntityId, 200, format);
            var wrongType = () => ids.Validate(original.EntityId, new EntityTypeId(201, 5), format);
            var outOfRange = () => ids.Validate(original.EntityId, new EntityTypeId(200, 128), format);
            wrongPartition.Should().Throw<ValidationException>();
            wrongApp.Should().Throw<ValidationException>();
            wrongType.Should().Throw<ValidationException>();
            outOfRange.Should().Throw<ArgumentOutOfRangeException>();
        }

        ids.Parse(null, format).Should().BeNull();
        ids.Validate((Guid?)null, 200, format).Should().BeNull();
        ids.Validate<SampleApp5>((Guid?)null, 200, format).Should().BeNull();
        ids.Validate((Guid?)null, new EntityTypeId(200, 128), format).Should().BeNull();
        ids.Validate<XEntity>((Guid?)null, format).Should().BeNull();
        operations.Parse((Guid?)null, format).Should().BeNull();
        operations.Validate((Guid?)null, 200, format).Should().BeNull();
        operations.Validate<SampleApp5>((Guid?)null, 200, format).Should().BeNull();
        operations.Validate((Guid?)null, new EntityTypeId(200, 128), format).Should().BeNull();
        operations.Validate<XEntity>((Guid?)null, format).Should().BeNull();
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Domain_Guid_Helpers_Should_Forward_Formats_And_Preserve_Partition_Checks(bool configuredSecure)
    {
        using var settings = SettingsProvider.Development<SampleApp5>(new
        {
            NexusAppSettings = new { UseSecureSourceKnownIds = configuredSecure }
        });
        using var ids = new SourceKnownEntityIdUtils(settings, new SourceKnownIdUtils(settings));
        var expected = SourceKnownEntity.GetEntityTypeId<XEntity>();
        var numericId = long.MinValue | (5L << 24);
        var plain = ids.GeneratePlain(numericId, expected);
        var secure = ids.GenerateSecure(numericId, expected);
        var entity = new XEntity(numericId) { EntityIdOps = ids, EntityIdSource = plain };
        foreach (var format in new SourceKnownEntityIdFormat?[] { null, SourceKnownEntityIdFormat.Secure, SourceKnownEntityIdFormat.Plain, SourceKnownEntityIdFormat.Auto })
        {
            foreach (var original in new[] { plain, secure })
            {
                var accepted = ids.Parse(original.EntityId, format).Valid;
                Func<SourceKnownEntityId>[] calls =
                [
                    () => entity.GetEntityId(original.EntityId, format: format),
                    () => entity.GetEntityId((Guid?)original.EntityId, format: format)!.Value,
                    () => entity.GetEntityId(original.EntityId, expected, format),
                    () => entity.GetEntityId((Guid?)original.EntityId, expected, format)!.Value,
                    () => entity.GetEntityId<XEntity>(original.EntityId, format),
                    () => entity.GetEntityId<XEntity>((Guid?)original.EntityId, format)!.Value
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
                    }
                }
                entity.GetEntityId(original.EntityId, validate: false, format).Valid.Should().Be(accepted);
                var wrongPartition = () => entity.GetEntityId<XEntityInApp6>(original.EntityId, format);
                wrongPartition.Should().Throw<ValidationException>();
            }
            entity.GetEntityId((Guid?)null, format: format).Should().BeNull();
            entity.GetEntityId((Guid?)null, expected, format).Should().BeNull();
            entity.GetEntityId<XEntity>((Guid?)null, format).Should().BeNull();
        }
        entity.GetEntityId((configuredSecure ? secure : plain).EntityId).Valid.Should().BeTrue();
        var invalidFormat = (SourceKnownEntityIdFormat)3;
        Action[] invalidCalls =
        [
            () => entity.GetEntityId(plain.EntityId, format: invalidFormat),
            () => entity.GetEntityId((Guid?)null, format: invalidFormat),
            () => entity.GetEntityId(plain.EntityId, expected, invalidFormat),
            () => entity.GetEntityId((Guid?)null, expected, invalidFormat),
            () => entity.GetEntityId<XEntity>(plain.EntityId, invalidFormat),
            () => entity.GetEntityId<XEntity>((Guid?)null, invalidFormat)
        ];
        foreach (var call in invalidCalls)
            call.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("format");
    }

    [Theory]
    [DataInlineUnit(-1)]
    [DataInlineUnit(3)]
    [DataInlineUnit(int.MaxValue)]
    public void Undefined_Formats_Should_Throw_Including_For_Null_Inputs(int value)
    {
        using var settings = SettingsProvider.Development<SampleApp5>();
        using var implementation = new SourceKnownEntityIdUtils(settings, new SourceKnownIdUtils(settings));
        ISourceKnownEntityIdUtils ids = implementation;
        ISourceKnownEntityIdOperations operations = implementation;
        var format = (SourceKnownEntityIdFormat)value;
        var expected = new EntityTypeId(200, 5);
        var valid = ids.GenerateSecure(long.MinValue | (5L << 24), expected).EntityId;

        foreach (var input in new Guid?[] { null, Guid.Empty, valid })
        {
            Action[] calls =
            [
                () => ids.Parse(input, format),
                () => ids.Validate(input, 200, format),
                () => ids.Validate<SampleApp5>(input, 200, format),
                () => ids.Validate(input, expected, format),
                () => ids.Validate<XEntity>(input, format),
                () => operations.Parse(input, format),
                () => operations.Validate(input, 200, format),
                () => operations.Validate<SampleApp5>(input, 200, format),
                () => operations.Validate(input, expected, format),
                () => operations.Validate<XEntity>(input, format)
            ];
            foreach (var call in calls)
                call.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("format");
            if (!input.HasValue) continue;
            Action[] nonNullableCalls =
            [
                () => operations.Parse(input.Value, format),
                () => ids.Parse(input.Value, format),
                () => ids.Validate(input.Value, 200, format),
                () => ids.Validate<SampleApp5>(input.Value, 200, format),
                () => ids.Validate(input.Value, expected, format),
                () => ids.Validate<XEntity>(input.Value, format),
                () => operations.Validate(input.Value, 200, format),
                () => operations.Validate<SampleApp5>(input.Value, 200, format),
                () => operations.Validate(input.Value, expected, format),
                () => operations.Validate<XEntity>(input.Value, format)
            ];
            foreach (var call in nonNullableCalls)
                call.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("format");
        }
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Formats_And_Conversions_Should_Preserve_Default_And_Fallback_Keys(bool configuredSecure)
    {
        using var settings = SettingsProvider.Development<SampleApp5>(new
        {
            NexusAppSettings = new
            {
                UseSecureSourceKnownIds = configuredSecure,
                Keys = new[] { new NexusKey(new string('A', 32)), new NexusKey(new string('B', 32)) { Default = true } }
            }
        });
        using var reader = new SourceKnownEntityIdUtils(settings, new SourceKnownIdUtils(settings));
        var expected = new EntityTypeId(200, 5);
        var numericId = long.MinValue | (5L << 24);
        foreach (var key in new[] { 'A', 'B' })
        {
            using var writerSettings = SettingsProvider.Development<SampleApp5>(new
            {
                NexusAppSettings = new { Keys = new[] { new NexusKey(new string(key, 32)) { Default = true } } }
            });
            using var writer = new SourceKnownEntityIdUtils(writerSettings, new SourceKnownIdUtils(writerSettings));
            var plain = writer.GeneratePlain(numericId, expected);
            var secure = writer.GenerateSecure(numericId, expected);
            foreach (var original in new[] { plain, secure })
            {
                var format = original.Secure ? SourceKnownEntityIdFormat.Secure : SourceKnownEntityIdFormat.Plain;
                reader.Validate(original.EntityId, expected, format).Source.Id.Should().Be(numericId);
                reader.Validate(original.EntityId, expected, SourceKnownEntityIdFormat.Auto).Secure.Should().Be(original.Secure);
                reader.Parse(original.EntityId).Valid.Should().Be(original.Secure == configuredSecure);
                reader.Parse(original.EntityId, original.Secure ? SourceKnownEntityIdFormat.Plain : SourceKnownEntityIdFormat.Secure)
                    .Valid.Should().BeFalse();
            }

            reader.ToSecure(secure).Should().Be(secure);
            reader.ToPlain(plain).Should().Be(plain);
            reader.ToSecure(plain).Should().Be(reader.GenerateSecure(numericId, expected));
            reader.ToPlain(secure).Should().Be(reader.GeneratePlain(numericId, expected));
            reader.ToSecure((SourceKnownEntityId?)plain).Should().Be(reader.GenerateSecure(numericId, expected));
            reader.ToPlain((SourceKnownEntityId?)secure).Should().Be(reader.GeneratePlain(numericId, expected));
            var correctedSecure = reader.ToSecure(plain with { Secure = true });
            correctedSecure.Should().Be(reader.GenerateSecure(numericId, expected));
            correctedSecure.Secure.Should().BeTrue();
            var correctedPlain = reader.ToPlain(secure with { Secure = false });
            correctedPlain.Should().Be(reader.GeneratePlain(numericId, expected));
            correctedPlain.Secure.Should().BeFalse();
            reader.ToSecure(null).Should().BeNull();
            reader.ToPlain(null).Should().BeNull();
            if (key == 'B')
            {
                reader.GeneratePlain(numericId, expected).Should().Be(plain);
                reader.GenerateSecure(numericId, expected).Should().Be(secure);
            }
            else
            {
                using var removedSettings = SettingsProvider.Development<SampleApp5>(new
                {
                    NexusAppSettings = new { Keys = new[] { new NexusKey(new string('B', 32)) { Default = true } } }
                });
                using var removed = new SourceKnownEntityIdUtils(removedSettings, new SourceKnownIdUtils(removedSettings));
                removed.Parse(plain.EntityId, SourceKnownEntityIdFormat.Plain).Valid.Should().BeFalse();
                removed.Parse(secure.EntityId, SourceKnownEntityIdFormat.Secure).Valid.Should().BeFalse();
                removed.Parse(plain.EntityId, SourceKnownEntityIdFormat.Auto).Valid.Should().BeFalse();
                removed.Parse(secure.EntityId, SourceKnownEntityIdFormat.Auto).Valid.Should().BeFalse();
            }
        }
    }

    [Fact]
    public void Plain_Should_Never_Decrypt_And_Auto_Should_Decrypt_After_Plain_Mac_Failure()
    {
        using var settings = SettingsProvider.Development<SampleApp5>(new
        {
            NexusAppSettings = new { Keys = new[] { new NexusKey(new string('A', 32)) { Default = true } } }
        });
        using var ids = new SourceKnownEntityIdUtils(settings, new SourceKnownIdUtils(settings));
        var plain = ids.GeneratePlain(long.MinValue, new EntityTypeId(1, 0));
        var markedCiphertext = plain.EntityId.ToByteArray(bigEndian: true);
        markedCiphertext[12] ^= 1;
        var marked = new Guid(markedCiphertext, bigEndian: true);
        var unmarked = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        ids.Parse(marked, SourceKnownEntityIdFormat.Plain).Valid.Should().BeFalse();
        ids.Parse(marked, SourceKnownEntityIdFormat.Auto).Valid.Should().BeFalse();

        // Every block is AES ciphertext. A disposed AES makes attempted decryption observable without collision searches.
        GetAes(ids).Dispose();
        ids.Parse(plain.EntityId, SourceKnownEntityIdFormat.Plain).Valid.Should().BeTrue();
        ids.Parse(plain.EntityId, SourceKnownEntityIdFormat.Auto).Valid.Should().BeTrue();
        foreach (var input in new[] { marked, unmarked })
        {
            ids.Parse(input, SourceKnownEntityIdFormat.Plain).Valid.Should().BeFalse();
            var auto = () => ids.Parse(input, SourceKnownEntityIdFormat.Auto);
            auto.Should().Throw<ObjectDisposedException>();
        }
        var secure = () => ids.Parse(plain.EntityId, SourceKnownEntityIdFormat.Secure);
        secure.Should().Throw<ObjectDisposedException>("Secure must skip even a valid plain MAC and attempt decryption");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_aes")]
    private static extern ref Aes256 GetAes(SourceKnownEntityIdUtils instance);

    [Theory]
    [DataInlineUnit(false, SourceKnownEntityIdFormat.Plain)]
    [DataInlineUnit(false, SourceKnownEntityIdFormat.Auto)]
    [DataInlineUnit(true, SourceKnownEntityIdFormat.Secure)]
    [DataInlineUnit(true, SourceKnownEntityIdFormat.Auto)]
    public void Formats_Should_Reject_Every_Tampered_Byte(bool secure, SourceKnownEntityIdFormat format)
    {
        using var settings = SettingsProvider.Development<SampleApp5>();
        using var ids = new SourceKnownEntityIdUtils(settings, new SourceKnownIdUtils(settings));
        var expected = new EntityTypeId(1, 0);
        var original = secure ? ids.GenerateSecure(long.MinValue, expected) : ids.GeneratePlain(long.MinValue, expected);
        ids.Validate(original.EntityId, expected, format).Valid.Should().BeTrue();
        for (var index = 0; index < 16; index++)
        {
            var bytes = original.EntityId.ToByteArray(bigEndian: true);
            bytes[index] ^= 1;
            var tampered = new Guid(bytes, bigEndian: true);
            ids.Parse(tampered, format).Valid.Should().BeFalse($"byte {index} was changed");
            var validate = () => ids.Validate(tampered, expected, format);
            validate.Should().Throw<ValidationException>();
            var toSecure = () => ids.ToSecure(original with { EntityId = tampered });
            var toPlain = () => ids.ToPlain(original with { EntityId = tampered });
            toSecure.Should().Throw<ValidationException>();
            toPlain.Should().Throw<ValidationException>();
        }
    }

    [Theory]
    [DataInlineUnit((byte)0x80)]
    [DataInlineUnit((byte)0x8E)]
    [DataInlineUnit((byte)0x8F)]
    [DataInlineUnit((byte)0xBF)]
    [DataInlineUnit((byte)0xC0)]
    public void Secure_And_Auto_Should_Reject_Authenticated_Variants_Without_Collision_Proof(byte variant)
    {
        using var settings = SettingsProvider.Development<SampleApp5>(new
        {
            NexusAppSettings = new { Keys = new[] { new NexusKey(new string('A', 32)) { Default = true } } }
        });
        using var ids = new SourceKnownEntityIdUtils(settings, new SourceKnownIdUtils(settings));
        var key = settings.NexusAppSettings.GetDefaultKey();
        using var aes = Aes.Create();
        aes.Key = key.EncryptionKey.Bytes;
        var bytes = ids.GeneratePlain(long.MinValue, new EntityTypeId(1, 0)).EntityId.ToByteArray(bigEndian: true);

        foreach (var candidate in new[] { (byte)(variant - 1), variant })
        {
            bytes[8] = candidate;
            bytes.AsSpan(12, 4).Clear();
            using var hasher = Blake3.Hasher.NewKeyed(key.MacKey.Bytes);
            hasher.Update(bytes);
            hasher.Finalize(bytes.AsSpan(12, 4));
            var ciphertext = new Guid(aes.EncryptEcb(bytes, PaddingMode.None), bigEndian: true);
            if (candidate != variant)
            {
                ids.Parse(ciphertext, SourceKnownEntityIdFormat.Plain).Valid.Should().BeFalse(
                    "the preceding variant must not satisfy the generation collision condition");
                continue;
            }

            ids.Parse(ciphertext, SourceKnownEntityIdFormat.Secure).Valid.Should().BeFalse();
            ids.Parse(ciphertext, SourceKnownEntityIdFormat.Auto).Valid.Should().BeFalse();
        }
    }

    [Theory]
    [DataInlineUnit(false, 0)]
    [DataInlineUnit(true, 0)]
    [DataInlineUnit(false, 5)]
    [DataInlineUnit(true, 5)]
    [DataInlineUnit(false, 127)]
    [DataInlineUnit(true, 127)]
    public void Generation_Should_Require_The_Explicit_Partition_Regardless_Of_Configured_AppId(bool secure, byte appId)
    {
        using var settings = SettingsProvider.Development<SampleApp6>(new
        {
            NexusAppSettings = new { UseSecureSourceKnownIds = secure }
        });
        var numericIds = new SourceKnownIdUtils(settings);
        using var implementation = new SourceKnownEntityIdUtils(settings, numericIds);
        ISourceKnownEntityIdUtils ids = implementation;
        ISourceKnownEntityIdOperations operations = implementation;
        var id = SourceKnownIdUtils.Generate<XEntity>(appId, 0);
        var expected = new EntityTypeId(200, appId);

        var generated = operations.Generate(id, expected);
        var plain = ids.GeneratePlain(id, expected);
        var encrypted = ids.GenerateSecure(id, expected);
        generated.EntityTypeId.Should().Be(expected);
        generated.Source.Id.Should().Be(id);
        generated.Secure.Should().Be(secure);
        plain.EntityTypeId.Should().Be(expected);
        plain.Source.Id.Should().Be(id);
        plain.Secure.Should().BeFalse();
        encrypted.EntityTypeId.Should().Be(expected);
        encrypted.Source.Id.Should().Be(id);
        encrypted.Secure.Should().BeTrue();
        generated.EntityId.Should().Be(secure ? encrypted.EntityId : plain.EntityId);

        var wrongPartition = new EntityTypeId(200, 6);
        var generateMismatch = () => operations.Generate(id, wrongPartition);
        var plainMismatch = () => ids.GeneratePlain(id, wrongPartition);
        var secureMismatch = () => ids.GenerateSecure(id, wrongPartition);
        generateMismatch.Should().Throw<ValidationException>();
        plainMismatch.Should().Throw<ValidationException>();
        secureMismatch.Should().Throw<ValidationException>();
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Validation_Uses_Configured_Partition_Entity_Metadata_Or_Explicit_Identity(bool secure)
    {
        using var settings = SettingsProvider.Development<SampleApp5>(new
        {
            NexusAppSettings = new { UseSecureSourceKnownIds = secure }
        });
        var numericIds = new SourceKnownIdUtils(settings);
        using var implementation = new SourceKnownEntityIdUtils(settings, numericIds);
        ISourceKnownEntityIdUtils ids = implementation;
        var primaryId = SourceKnownIdUtils.Generate<XEntity>(5, 0);
        var secondaryId = SourceKnownIdUtils.Generate<XEntityInApp6>(6, 0);
        var primary = secure ? ids.GenerateSecure<XEntity>(primaryId) : ids.GeneratePlain<XEntity>(primaryId);
        var secondary = secure ? ids.GenerateSecure<XEntityInApp6>(secondaryId) : ids.GeneratePlain<XEntityInApp6>(secondaryId);

        ids.Validate<XEntity>(primary.EntityId).Valid.Should().BeTrue();
        ids.Validate(primary.EntityId, 200).Valid.Should().BeTrue();
        ids.Validate((Guid?)primary.EntityId, 200)!.Value.Valid.Should().BeTrue();
        var wrongDefault = () => ids.Validate(secondary.EntityId, 200);
        wrongDefault.Should().Throw<ValidationException>();
        var wrongNullableDefault = () => ids.Validate((Guid?)secondary.EntityId, 200);
        wrongNullableDefault.Should().Throw<ValidationException>();
        ids.Validate<XEntityInApp6>(secondary.EntityId).Valid.Should().BeTrue();
        ids.Validate<XEntityInApp6>((Guid?)secondary.EntityId)!.Value.Valid.Should().BeTrue();

        ids.Validate<SampleApp6>(secondary.EntityId, 200).Valid.Should().BeTrue();
        ids.Validate(secondary.EntityId, new EntityTypeId(200, 6)).Valid.Should().BeTrue();
        ids.Validate((Guid?)secondary.EntityId, new EntityTypeId(200, 6))!.Value.Valid.Should().BeTrue();
        ids.Validate<SampleApp6>((Guid?)secondary.EntityId, 200)!.Value.Valid.Should().BeTrue();
        var wrongCustom = () => ids.Validate<SampleApp5>(secondary.EntityId, 200);
        wrongCustom.Should().Throw<ValidationException>();
        var conflictingCustom = () => ids.Validate(secondary.EntityId, new EntityTypeId(200, 5));
        conflictingCustom.Should().Throw<ValidationException>();
        var wrongEntityType = () => ids.Validate<SampleApp6>(secondary.EntityId, 201);
        wrongEntityType.Should().Throw<ValidationException>();
        var invalidGuid = () => ids.Validate<XEntityInApp6>(Guid.Empty);
        invalidGuid.Should().Throw<ValidationException>();
        var invalidComposite = () => ids.Validate(Guid.Empty, new EntityTypeId(200, 6));
        invalidComposite.Should().Throw<ValidationException>();
        var wrongCompositeType = () => ids.Validate(secondary.EntityId, new EntityTypeId(201, 6));
        wrongCompositeType.Should().Throw<ValidationException>();
        var outOfRangePartition = () => ids.Validate(secondary.EntityId, new EntityTypeId(200, 128));
        outOfRangePartition.Should().Throw<ArgumentOutOfRangeException>();

        ids.Validate((Guid?)null, 200).Should().BeNull();
        ids.Validate<XEntity>((Guid?)null).Should().BeNull();
        ids.Validate<SampleApp6>((Guid?)null, 200).Should().BeNull();
        ids.Validate((Guid?)null, new EntityTypeId(200, 6)).Should().BeNull();
    }

    [Theory]
    [DataInlineUnit((byte)1)]
    [DataInlineUnit((byte)255)]
    public void Unsupported_Epoch_Is_Rejected_Even_With_Valid_Mac_And_Encryption(DrnTestContextUnit context, byte epoch)
    {
        var utils = context.GetRequiredService<ISourceKnownEntityIdUtils>();
        var settings = context.GetRequiredService<IAppSettings>();
        var plain = utils.GeneratePlain(long.MinValue, new EntityTypeId(1, 0));
        var bytes = plain.EntityId.ToByteArray(bigEndian: true);
        bytes[0] = epoch;
        bytes.AsSpan(12, 4).Clear();
        using (var hasher = Blake3.Hasher.NewKeyed(settings.NexusAppSettings.GetDefaultKey().MacKey.Bytes))
        {
            hasher.Update(bytes);
            hasher.Finalize(bytes.AsSpan(12, 4));
        }
        var unsupported = new Guid(bytes, bigEndian: true);
        utils.Parse(unsupported, SourceKnownEntityIdFormat.Plain).Valid.Should().BeFalse();
        var validate = () => utils.Validate(unsupported, 1, SourceKnownEntityIdFormat.Plain);
        validate.Should().Throw<Exception>();
        var convert = () => utils.ToSecure(plain with { EntityId = unsupported });
        convert.Should().Throw<Exception>();

        using var aes = Aes.Create();
        aes.Key = settings.NexusAppSettings.GetDefaultKey().EncryptionKey.Bytes;
        var cipher = aes.EncryptEcb(bytes, PaddingMode.None);
        utils.Parse(new Guid(cipher, bigEndian: true)).Valid.Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit]
    public void Historical_Ids_Below_Trusted_Floor_Remain_Readable_And_Convertible(DrnTestContextUnit context)
    {
        var utils = context.GetRequiredService<ISourceKnownEntityIdUtils>();
        var plain = utils.GeneratePlain(long.MinValue, new EntityTypeId(1, 0));
        plain.Source.CreatedAt.Should().BeBefore(SourceKnownGenerationTime.Policy.MinimumUtc);
        utils.Parse(plain.EntityId, SourceKnownEntityIdFormat.Plain).Valid.Should().BeTrue();
        var secure = utils.ToSecure(plain);
        utils.Parse(secure.EntityId).Source.CreatedAt.Should().Be(plain.Source.CreatedAt);
        utils.ToPlain(secure).EntityId.Should().Be(plain.EntityId);
        var secondHalf = utils.GeneratePlain(0, new EntityTypeId(1, 0));
        utils.Parse(secondHalf.EntityId, SourceKnownEntityIdFormat.Plain).Valid.Should().BeTrue();
        utils.Parse(utils.ToSecure(secondHalf).EntityId).Source.Id.Should().Be(0);
    }

    [Theory]
    [DataInlineUnit]
    public async Task ExplicitMethods_Should_Generate_Valid_Secure_And_Plain_Ids(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = 5, AppInstanceId = 12 };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });

        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();
        var xEntityType = SourceKnownEntity.GetEntityType<XEntity>();

        var epoch = EpochTimeUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay); // buffer for TimeStampManager to populate a valid tick
        var longId1 = idUtils.Next<XEntity>();
        var entity1 = new XEntity(longId1);
        var entity1Dup = new XEntity(longId1);

        // Generate both variants from same source ID
        var plainId1 = entityIdUtils.GeneratePlain(entity1);
        var secureId1 = entityIdUtils.GenerateSecure(entity1Dup);
        entity1.EntityIdSource = plainId1;
        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay);
        var afterIdGenerated = DateTimeOffset.UtcNow;

        // Assert both are valid with correct properties
        AssertValidEntityId(plainId1, longId1, nexusSettings, xEntityType, expectedSecure: false);
        AssertValidEntityId(secureId1, longId1, nexusSettings, xEntityType, expectedSecure: true);

        // Parse roundtrip — both variants
        entityIdUtils.Parse(null).Should().BeNull();
        AssertParseRoundTrip(entityIdUtils, plainId1);
        AssertParseRoundTrip(entityIdUtils, secureId1);

        // Detailed parse equality assertions
        var parsedPlain = entityIdUtils.Parse(plainId1.EntityId, SourceKnownEntityIdFormat.Plain);
        parsedPlain.Should().Be(plainId1);
        parsedPlain.EntityId.Should().Be(plainId1.EntityId);

        // Timestamp assertions via parsed source id
        var idInfo = idUtils.Parse(plainId1.Source.Id);
        idInfo.AppId.Should().Be(nexusSettings.AppId);
        idInfo.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);
        AssertCreatedAtWithinGeneratedRange(idInfo, beforeIdGenerated, afterIdGenerated, epoch);
        epoch.Should().BeBefore(beforeIdGenerated);

        // Secure parse — parsed result carries the encrypted EntityId Guid
        var parsedSecure = entityIdUtils.Parse(secureId1.EntityId);
        parsedSecure.Should().Be(secureId1);
        parsedSecure.EntityId.Should().Be(secureId1.EntityId);

        // Secure and plain parse to same source but different GUIDs
        parsedPlain.Source.Should().Be(parsedSecure.Source);
        parsedPlain.EntityType.Should().Be(parsedSecure.EntityType);
        plainId1.EntityId.Should().NotBe(secureId1.EntityId);

        // Ordering and comparison operators
        var longId2 = idUtils.Next<XEntity>();
        var plainId2 = entityIdUtils.GeneratePlain(new XEntity(longId2));
        var plainId1Again = entityIdUtils.GeneratePlain(new XEntity(longId1));
        (plainId2 > plainId1).Should().BeTrue();
        (plainId2 >= plainId1).Should().BeTrue();

        var plainId2Dup = entityIdUtils.GeneratePlain(new XEntity(longId2));
        (plainId2 >= plainId2Dup).Should().BeTrue();
        (plainId1 < plainId2).Should().BeTrue();
        (plainId1 <= plainId2).Should().BeTrue();
        (plainId1 <= plainId1Again).Should().BeTrue();

        // Same entity type check
        plainId1.HasSameEntityType(plainId2).Should().BeTrue();
        secureId1.HasSameEntityType(entityIdUtils.GenerateSecure(new XEntity(longId2))).Should().BeTrue();

        // Cross entity type
        var plainY = entityIdUtils.GeneratePlain(new YEntity(longId2));
        plainId2.HasSameEntityType(plainY).Should().BeFalse();

        // Generic overloads
        var secureGeneric = entityIdUtils.GenerateSecure<XEntity>(longId1);
        AssertValidEntityId(secureGeneric, longId1, nexusSettings, xEntityType, expectedSecure: true);

        var plainGeneric = entityIdUtils.GeneratePlain<XEntity>(longId1);
        AssertValidEntityId(plainGeneric, longId1, nexusSettings, xEntityType, expectedSecure: false);

        // Self-generating parameterless overloads
        var selfGeneratedPlain = entityIdUtils.GeneratePlain<XEntity>();
        AssertValidEntityId(selfGeneratedPlain, selfGeneratedPlain.Source.Id, nexusSettings, xEntityType, expectedSecure: false);

        var selfGeneratedSecure = entityIdUtils.GenerateSecure<XEntity>();
        AssertValidEntityId(selfGeneratedSecure, selfGeneratedSecure.Source.Id, nexusSettings, xEntityType, expectedSecure: true);

        var selfGeneratedDispatched = entityIdUtils.Generate<XEntity>();
        AssertValidEntityId(selfGeneratedDispatched, selfGeneratedDispatched.Source.Id, nexusSettings, xEntityType, expectedSecure: nexusSettings.UseSecureSourceKnownIds);
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public async Task Generate_Should_Dispatch_Based_On_UseSecureFlag(DrnTestContextUnit context, bool secure)
    {
        var nexusSettings = new NexusAppSettings
        {
            AppId = 5,
            AppInstanceId = 12,
            UseSecureSourceKnownIds = secure
        };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay); // buffer for TimeStampManager to populate a valid tick
        var longId = idUtils.Next<XEntity>();

        // Generate() should dispatch to secure/plain path based on flag
        var entityId = entityIdUtils.Generate(new XEntity(longId));
        AssertValidEntityId(entityId, longId, nexusSettings, SourceKnownEntity.GetEntityType<XEntity>(), expectedSecure: secure);
        AssertParseRoundTrip(entityIdUtils, entityId);

        // Parameterless generic Generate<TEntity>() should also dispatch based on flag
        var entityIdGeneric = entityIdUtils.Generate<XEntity>();
        AssertValidEntityId(entityIdGeneric, entityIdGeneric.Source.Id, nexusSettings, SourceKnownEntity.GetEntityType<XEntity>(), expectedSecure: secure);
        AssertParseRoundTrip(entityIdUtils, entityIdGeneric);

        // Explicit methods should always bypass the flag
        var explicitPlain = entityIdUtils.GeneratePlain(new XEntity(longId));
        explicitPlain.Secure.Should().BeFalse("GeneratePlain should always set Secure=false");
        AssertPlaintextMarkers(explicitPlain, true, "GeneratePlain should always produce plaintext markers");
        AssertParseRoundTrip(entityIdUtils, explicitPlain);

        var explicitSecure = entityIdUtils.GenerateSecure(new XEntity(longId));
        explicitSecure.Secure.Should().BeTrue("GenerateSecure should always set Secure=true");
        AssertParseRoundTrip(entityIdUtils, explicitSecure);
    }

    [Theory]
    [DataInlineUnit]
    public async Task SecureParse_Should_Fail_On_Tampered_Guid(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = 5, AppInstanceId = 12 };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay); // buffer for TimeStampManager to populate a valid tick
        var longId = idUtils.Next<XEntity>();
        var secureEntityId = entityIdUtils.GenerateSecure(new XEntity(longId));

        // Tamper with first byte — AES-ECB decryption produces garbage, markers won't match
        var tamperedBytes = secureEntityId.EntityId.ToByteArray(bigEndian: true);
        tamperedBytes[0] ^= 0xFF;
        var tamperedResult1 = entityIdUtils.Parse(new Guid(tamperedBytes, bigEndian: true));
        tamperedResult1.Valid.Should().BeFalse("tampered secure guid should be detected as invalid");
        tamperedResult1.Secure.Should().BeFalse("invalid result should have Secure=false");

        // Tamper with a different byte position
        var tamperedBytes2 = secureEntityId.EntityId.ToByteArray(bigEndian: true);
        tamperedBytes2[4] ^= 0x01;
        var tamperedResult2 = entityIdUtils.Parse(new Guid(tamperedBytes2, bigEndian: true));
        tamperedResult2.Valid.Should().BeFalse("any tampered byte should be detected as invalid");
        tamperedResult2.Secure.Should().BeFalse("invalid result should have Secure=false");
    }

    [Theory]
    [DataInlineUnit]
    public async Task SecureValidate_Should_Work_With_Multiple_EntityTypes(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = 5, AppInstanceId = 12 };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay); // buffer for TimeStampManager to populate a valid tick
        var longId = idUtils.Next<XEntity>();

        var secureX = entityIdUtils.GenerateSecure<XEntity>(longId);
        var secureY = entityIdUtils.GenerateSecure<YEntity>(longId);

        // Different entity types should produce different encrypted guids
        secureX.EntityId.Should().NotBe(secureY.EntityId);
        secureX.EntityType.Should().NotBe(secureY.EntityType);

        // Validate should work correctly for matching types
        var validatedX = entityIdUtils.Validate<XEntity>(secureX.EntityId);
        validatedX.Valid.Should().BeTrue();
        validatedX.Secure.Should().BeTrue("validated secure id should preserve Secure=true");
        validatedX.EntityType.Should().Be(SourceKnownEntity.GetEntityType<XEntity>());

        var validatedY = entityIdUtils.Validate<YEntity>(secureY.EntityId);
        validatedY.Valid.Should().BeTrue();
        validatedY.Secure.Should().BeTrue("validated secure id should preserve Secure=true");
        validatedY.EntityType.Should().Be(SourceKnownEntity.GetEntityType<YEntity>());

        // HasSameEntityType and HasSameEntityTypeId should work correctly after parsing
        var parsedX = entityIdUtils.Parse(secureX.EntityId);
        var parsedY = entityIdUtils.Parse(secureY.EntityId);
        parsedX.Secure.Should().BeTrue("parsed secure id should have Secure=true");
        parsedY.Secure.Should().BeTrue("parsed secure id should have Secure=true");
        parsedX.HasSameEntityType(parsedY).Should().BeFalse();
        parsedX.HasSameEntityType<XEntity>().Should().BeTrue();
        parsedX.HasSameEntityType<XEntityInApp6>().Should().BeFalse("same entity byte from different AppId must not match");
        parsedX.HasSameEntityTypeId<XEntity>().Should().BeTrue();
        parsedX.HasSameEntityTypeId<XEntityInApp6>().Should().BeFalse("same entity byte from different AppId must not match EntityTypeId");
        parsedY.HasSameEntityType<YEntity>().Should().BeTrue();

        // Cross-type validation should throw
        var act = () => entityIdUtils.Validate<YEntity>(secureX.EntityId);
        act.Should().Throw<ValidationException>();

        // Cross-partition validation with same EntityType byte should throw
        var actCrossPartition = () => entityIdUtils.Validate<XEntityInApp6>(secureX.EntityId);
        actCrossPartition.Should().Throw<ValidationException>();
    }

    [Theory]
    [DataInlineUnit]
    public async Task ToSecure_And_ToPlain_Should_Convert_And_Be_Idempotent(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = 5, AppInstanceId = 12 };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay); // buffer for TimeStampManager to populate a valid tick
        var longId = idUtils.Next<XEntity>();

        var plainId = entityIdUtils.GeneratePlain(new XEntity(longId));
        var secureId = entityIdUtils.GenerateSecure(new XEntity(longId));

        // Plain → Secure conversion
        var convertedToSecure = entityIdUtils.ToSecure(plainId);
        convertedToSecure.Secure.Should().BeTrue("converted id should be secure");
        convertedToSecure.Source.Should().Be(plainId.Source, "Source must be preserved");
        convertedToSecure.EntityType.Should().Be(plainId.EntityType, "EntityType must be preserved");
        convertedToSecure.Valid.Should().BeTrue("converted id must be valid");
        convertedToSecure.EntityId.Should().Be(secureId.EntityId, "converted GUID must match directly-generated secure GUID");
        AssertParseRoundTrip(entityIdUtils, convertedToSecure);

        // Secure → Plain conversion
        var convertedToPlain = entityIdUtils.ToPlain(secureId);
        convertedToPlain.Secure.Should().BeFalse("converted id should be plain");
        convertedToPlain.Source.Should().Be(secureId.Source, "Source must be preserved");
        convertedToPlain.EntityType.Should().Be(secureId.EntityType, "EntityType must be preserved");
        convertedToPlain.Valid.Should().BeTrue("converted id must be valid");
        convertedToPlain.EntityId.Should().Be(plainId.EntityId, "converted GUID must match directly-generated plain GUID");
        AssertParseRoundTrip(entityIdUtils, convertedToPlain);

        // Idempotency — ToSecure on already-secure returns same
        var secureAgain = entityIdUtils.ToSecure(secureId);
        secureAgain.Should().Be(secureId, "ToSecure on secure id should return same id");

        // Idempotency — ToPlain on already-plain returns same
        var plainAgain = entityIdUtils.ToPlain(plainId);
        plainAgain.Should().Be(plainId, "ToPlain on plain id should return same id");

        // Nullable overloads
        entityIdUtils.ToSecure(null).Should().BeNull();
        entityIdUtils.ToPlain(null).Should().BeNull();
        entityIdUtils.ToSecure((SourceKnownEntityId?)plainId).Should().Be(convertedToSecure);
        entityIdUtils.ToPlain((SourceKnownEntityId?)secureId).Should().Be(convertedToPlain);

        // Invalid id should throw
        var invalidId = new SourceKnownEntityId(default, Guid.NewGuid(), byte.MaxValue, false, Secure: false);
        var actSecure = () => entityIdUtils.ToSecure(invalidId);
        actSecure.Should().Throw<ValidationException>();
        var actPlain = () => entityIdUtils.ToPlain(invalidId);
        actPlain.Should().Throw<ValidationException>();
    }

    [Theory]
    [DataInlineUnit]
    public async Task Parse_Should_Reject_Tampered_Variant_Byte(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = 5, AppInstanceId = 12 };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay);
        var longId = idUtils.Next<XEntity>();
        var secureId = entityIdUtils.GenerateSecure(new XEntity(longId));

        // Decrypt the secure SKEID to access plaintext
        var cipherBytes = secureId.EntityId.ToByteArray(bigEndian: true);
        var aesKey = context.GetRequiredService<IAppSettings>()
            .NexusAppSettings.GetDefaultKey().EncryptionKey.Bytes;

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = aesKey;

        var plainBytes = new byte[16];
        aes.DecryptEcb(cipherBytes, plainBytes, PaddingMode.None);

        // The default variant should be 0x8D at byte 8 (RFC 9562 octet 8) — tamper it to 0x8F
        // (0x8F implies two successive collisions at 0x8D and 0x8E, which didn't actually happen)
        plainBytes[8].Should().Be(0x8D, "default variant byte should be 0x8D");
        plainBytes[8] = 0x8F; // tampered — no genuine collision at 0x8E

        // Re-encrypt with tampered plaintext
        var tamperedCipher = new byte[16];
        aes.EncryptEcb(plainBytes, tamperedCipher, PaddingMode.None);
        var tamperedGuid = new Guid(tamperedCipher, bigEndian: true);

        // Parse must reject: backward verification will reconstruct variant=0x8E,
        // encrypt it, and find that its ciphertext does NOT have marker+MAC coincidence
        var result = entityIdUtils.Parse(tamperedGuid);
        result.Valid.Should().BeFalse("tampered variant byte must be rejected by backward verification");
    }

    [Theory]
    [DataInlineUnit]
    public async Task PlainParse_Should_Fail_On_Any_Tampered_Payload_Byte(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = 5, AppInstanceId = 12 };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay);
        var longId = idUtils.Next<XEntity>();

        // Generate a Plain SKEID so we can directly modify the byte layout and test MAC verification.
        var plainId = entityIdUtils.GeneratePlain(new XEntity(longId));
        var originalBytes = plainId.EntityId.ToByteArray(bigEndian: true);

        // Bytes 0-11 are the SKEID payload (timestamp, id, entityType, markers, etc.).
        // Bytes 12-15 are the MAC. We test that tampering any byte in 0-11 triggers MAC rejection.
        for (var i = 0; i < 12; i++)
        {
            var tamperedBytes = originalBytes.ToArray();
            tamperedBytes[i] ^= 0xFF; // Flip bits

            var tamperedGuid = new Guid(tamperedBytes, bigEndian: true);
            var tamperedResult = entityIdUtils.Parse(tamperedGuid, SourceKnownEntityIdFormat.Plain);

            tamperedResult.Valid.Should().BeFalse($"tampering payload byte at index {i} must be rejected by MAC verification");
        }
    }

    [Fact]
    public void Parse_Should_Fall_Back_To_Previous_KeyRing_Entries()
    {
        var oldKey = new NexusKey(new string('A', 32)) { Default = true };
        using var oldSettings = SettingsProvider.Development<SampleApp5>(new
        {
            NexusAppSettings = new NexusAppSettings
            {
                AppId = 5,
                AppInstanceId = 12,
                Keys = [oldKey]
            }
        });
        var oldIdUtils = new SourceKnownIdUtils(oldSettings);
        using var oldEntityIdUtils = new SourceKnownEntityIdUtils(oldSettings, oldIdUtils);

        var id = SourceKnownIdUtils.Generate<XEntity>(oldSettings.NexusAppSettings.AppId, oldSettings.NexusAppSettings.AppInstanceId);
        var secureId = oldEntityIdUtils.GenerateSecure<XEntity>(id);

        using var rotatedSettings = SettingsProvider.Development<SampleApp5>(new
        {
            NexusAppSettings = new NexusAppSettings
            {
                AppId = 5,
                AppInstanceId = 12,
                Keys =
                [
                    new NexusKey(new string('B', 32)) { Default = true },
                    new NexusKey(new string('A', 32))
                ]
            }
        });
        var rotatedIdUtils = new SourceKnownIdUtils(rotatedSettings);
        using var rotatedEntityIdUtils = new SourceKnownEntityIdUtils(rotatedSettings, rotatedIdUtils);

        var parsed = rotatedEntityIdUtils.Parse(secureId.EntityId);

        parsed.Valid.Should().BeTrue("old IDs must remain parseable while the old key remains in the key ring");
        parsed.Secure.Should().BeTrue();
        parsed.EntityType.Should().Be(SourceKnownEntity.GetEntityType<XEntity>());
        parsed.Source.Id.Should().Be(id);
    }

    [Fact]
    public void Generate_Should_Use_Default_Key_When_Default_Is_Not_First()
    {
        using var settings = SettingsProvider.Development<SampleApp5>(new
        {
            NexusAppSettings = new NexusAppSettings
            {
                AppId = 5,
                AppInstanceId = 12,
                Keys =
                [
                    new NexusKey(new string('A', 32)),
                    new NexusKey(new string('B', 32)) { Default = true }
                ]
            }
        });
        var idUtils = new SourceKnownIdUtils(settings);
        using var entityIdUtils = new SourceKnownEntityIdUtils(settings, idUtils);

        var id = SourceKnownIdUtils.Generate<XEntity>(settings.NexusAppSettings.AppId, settings.NexusAppSettings.AppInstanceId);
        var secureId = entityIdUtils.GenerateSecure<XEntity>(id);

        using var defaultOnlySettings = SettingsProvider.Development<SampleApp5>(new
        {
            NexusAppSettings = new NexusAppSettings
            {
                AppId = 5,
                AppInstanceId = 12,
                Keys = [new NexusKey(new string('B', 32)) { Default = true }]
            }
        });
        var defaultOnlyIdUtils = new SourceKnownIdUtils(defaultOnlySettings);
        using var defaultOnlyEntityIdUtils = new SourceKnownEntityIdUtils(defaultOnlySettings, defaultOnlyIdUtils);

        var parsed = defaultOnlyEntityIdUtils.Parse(secureId.EntityId);

        parsed.Valid.Should().BeTrue("generation must use the configured default key even when it is not first in Keys");
        parsed.Source.Id.Should().Be(id);
    }

    [Fact]
    public void Validate_With_Same_EntityType_Byte_Across_Different_AppIds_Should_Throw_ValidationException()
    {
        using var settings = SettingsProvider.Development<SampleApp5>(new
        {
            NexusAppSettings = new NexusAppSettings
            {
                AppId = 5,
                AppInstanceId = 1
            }
        });
        var idUtils = new SourceKnownIdUtils(settings);
        using var entityIdUtils = new SourceKnownEntityIdUtils(settings, idUtils);

        // Generate ID for XEntity (AppId=5, EntityType=200)
        var entity5 = new XEntity(idUtils.Next<XEntity>());
        var id5 = entityIdUtils.Generate(entity5);

        // Validation against AppId=5 entity type succeeds
        var validResult = entityIdUtils.Validate<XEntity>(id5.EntityId);
        validResult.Valid.Should().BeTrue();
        validResult.EntityType.Should().Be(200);
        validResult.Source.AppId.Should().Be(5);

        // Explicit typed validation against (EntityType=200, AppId=5) succeeds
        var validComposite = entityIdUtils.Validate<SampleApp5>(id5.EntityId, 200);
        validComposite.Valid.Should().BeTrue();

        // Cross-partition validation: XEntityInApp6 has (EntityType=200, AppId=6)
        // The incoming ID must agree with the entity declaration.
        var actGeneric = () => entityIdUtils.Validate<XEntityInApp6>(id5.EntityId);
        actGeneric.Should().Throw<ValidationException>();

        // Explicit typed validation against (EntityType=200, AppId=6) must throw
        var actComposite = () => entityIdUtils.Validate<SampleApp6>(id5.EntityId, 200);
        actComposite.Should().Throw<ValidationException>();

        // SourceKnownEntity.GetEntityId<TEntity> partition validation
        var entityInstance = new XEntity(idUtils.Next<XEntity>());
        entityInstance.EntityIdSource = entityIdUtils.Generate(entityInstance);
        entityInstance.EntityIdOps = entityIdUtils;

        // Same partition succeeds (Guid and long overloads)
        var validEntityGet = entityInstance.GetEntityId<XEntity>(id5.EntityId);
        validEntityGet.Valid.Should().BeTrue();
        var validEntityGetLong = entityInstance.GetEntityId<XEntity>(id5.Source.Id);
        validEntityGetLong.Valid.Should().BeTrue();
        entityInstance.GetEntityId((long?)id5.Source.Id, SourceKnownEntity.GetEntityTypeId<XEntity>())!.Value.Valid.Should().BeTrue();
        entityInstance.GetEntityId(id5.Source.Id, SourceKnownEntity.GetEntityTypeId<XEntity>()).Valid.Should().BeTrue();
        var expected = new EntityTypeId(200, SampleApp5.AppId);
        entityInstance.GetEntityId(id5.EntityId, expected).Should().Be(id5);
        entityInstance.GetEntityId((Guid?)id5.EntityId, expected)!.Value.Should().Be(id5);
        entityInstance.GetEntityId((Guid?)null, expected).Should().BeNull();
        entityInstance.GetEntityId((long?)null, expected).Should().BeNull();

        // Cross-partition GetEntityId<TEntity> throws ValidationException (Guid and long)
        var actEntityGet = () => entityInstance.GetEntityId<XEntityInApp6>(id5.EntityId);
        actEntityGet.Should().Throw<ValidationException>();
        var actEntityGetLong = () => entityInstance.GetEntityId<XEntityInApp6>(id5.Source.Id);
        actEntityGetLong.Should().Throw<ValidationException>();
        var actCompositeLong = () => entityInstance.GetEntityId(id5.Source.Id, new EntityTypeId(200, 6));
        actCompositeLong.Should().Throw<ValidationException>();
        var actCompositeGuid = () => entityInstance.GetEntityId(id5.EntityId, new EntityTypeId(200, 6));
        actCompositeGuid.Should().Throw<ValidationException>();
        var actNullableCompositeGuid = () => entityInstance.GetEntityId((Guid?)id5.EntityId, new EntityTypeId(200, 6));
        actNullableCompositeGuid.Should().Throw<ValidationException>();
        var actNullableCompositeLong = () => entityInstance.GetEntityId((long?)id5.Source.Id, new EntityTypeId(200, 6));
        actNullableCompositeLong.Should().Throw<ValidationException>();

        // Generic Generate<TEntity>(long) must throw if long ID belongs to a different AppId partition
        var longIdApp6 = idUtils.Next<XEntityInApp6>();
        var actGenerateMismatch = () => entityIdUtils.Generate<XEntity>(longIdApp6);
        actGenerateMismatch.Should().Throw<ValidationException>();
    }

    private static void AssertValidEntityId(SourceKnownEntityId entityId, long expectedId,
        NexusAppSettings nexusSettings, byte expectedEntityType, bool expectedSecure)
    {
        entityId.Valid.Should().BeTrue();
        entityId.Source.Id.Should().Be(expectedId);
        entityId.Source.AppId.Should().Be(nexusSettings.AppId);
        entityId.Source.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);
        entityId.EntityType.Should().Be(expectedEntityType);
        entityId.Secure.Should().Be(expectedSecure);

        // Secure flag ↔ EntityId GUID content invariant:
        // Plain: plaintext GUID — deterministic markers (8D8D) and RFC 9562 V8 compliance
        // Secure:   AES-encrypted GUID — markers/V8 assertions skipped because ciphertext can
        //           coincidentally produce 8D8D marker bytes (~1/65536, see SourceKnownEntityIdUtils L169)
        if (expectedSecure) return;

        AssertPlaintextMarkers(entityId, true, "Plain EntityId must contain plaintext 8D8D markers");
    }

    private static void AssertParseRoundTrip(ISourceKnownEntityIdUtils entityIdUtils, SourceKnownEntityId original)
    {
        var parsed = entityIdUtils.Parse(original.EntityId, SourceKnownEntityIdFormat.Auto);
        parsed.Valid.Should().BeTrue();
        parsed.Source.Id.Should().Be(original.Source.Id);
        parsed.Source.Should().Be(original.Source);
        parsed.EntityType.Should().Be(original.EntityType);
        parsed.Secure.Should().Be(original.Secure);
    }

    private static void AssertPlaintextMarkers(SourceKnownEntityId entityId, bool shouldHaveMarkers, string because)
    {
        var hasMarkers = IsVersion8Rfc9562(entityId.EntityId);
        hasMarkers.Should().Be(shouldHaveMarkers, because);
    }

    private static bool IsVersion8Rfc9562(Guid guid)
    {
        // Use big-endian byte array for RFC 9562 standard octet positions
        var bytes = guid.ToByteArray(bigEndian: true);
        var isVersion8 = (bytes[6] >> 4) == 8; // RFC 9562 octet 6: version nibble
        var isRfc9562Variant = (bytes[8] & 0xC0) == 0x80; // RFC 9562 octet 8: variant bits
        return isVersion8 && isRfc9562Variant;
    }

    /// <summary>
    /// Validates that <paramref name="idInfo"/>.CreatedAt falls within the expected test execution range.
    /// Timestamps are converted to 250ms epoch ticks before assertion because TimeStampManager truncates
    /// timestamps to 250ms precision boundaries. Converting all bounds to ticks normalizes sub-second
    /// precision disparities and prevents race conditions with high-precision DateTimeOffset.UtcNow bounds.
    /// </summary>
    private static void AssertCreatedAtWithinGeneratedRange(
        SourceKnownId idInfo,
        DateTimeOffset beforeIdGenerated,
        DateTimeOffset afterIdGenerated,
        DateTimeOffset epoch)
    {
        var createdAtTimestamp = EpochTimeUtils.ConvertToTicks(idInfo.CreatedAt, epoch);
        createdAtTimestamp.Should().BeGreaterThanOrEqualTo(EpochTimeUtils.ConvertToTicks(beforeIdGenerated, epoch));
        createdAtTimestamp.Should().BeLessThanOrEqualTo(EpochTimeUtils.ConvertToTicks(afterIdGenerated, epoch));
    }


    public readonly struct SampleApp5 : IAppId
    {
        public const byte Value = 5;
        public static byte AppId => Value;
    }

    public readonly struct SampleApp6 : IAppId
    {
        public const byte Value = 6;
        public static byte AppId => Value;
    }

    [EntityType<SampleApp5>(200)]
    internal class XEntity(long id) : SourceKnownEntity(id);
    [EntityType<SampleApp5>(201)]
    internal class YEntity(long id) : SourceKnownEntity(id);

    [EntityType<SampleApp6>(200)]
    internal class XEntityInApp6(long id) : SourceKnownEntity(id);
}
