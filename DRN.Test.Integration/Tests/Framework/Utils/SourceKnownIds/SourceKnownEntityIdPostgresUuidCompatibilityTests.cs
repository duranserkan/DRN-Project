using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Testing.Contexts.Postgres;
using Npgsql;
using static DRN.Test.Integration.Tests.Framework.Utils.SourceKnownIds.IetfTestData;

namespace DRN.Test.Integration.Tests.Framework.Utils.SourceKnownIds;

/// <summary>
/// Verifies that PostgreSQL native <c>uuid</c> columns fully support both plaintext
/// <see cref="SourceKnownEntityId"/> (RFC 9562 UUIDv8) and AES-256 encrypted Secure
/// <see cref="SourceKnownEntityId"/>, ensuring that:
/// 1. PostgreSQL's UUID parser and storage engine accept both formats without rejection.
/// 2. Stored GUIDs are retrieved as <see cref="Guid"/> with bit-for-bit identity.
/// 3. Retrieved GUIDs successfully parse back into <see cref="SourceKnownEntityId"/> with intact metadata.
/// 4. SQL parameter filtering, indexed lookups, and text literal casting ('...'::uuid) operate accurately.
/// 5. Npgsql binary parameter passing, SQL string literal casting, indexed lookups, and batch queries correctly persist and filter both identifier formats.
/// Uses same test data from DRN.Test.Unit/Tests/Framework/Utils/Ids/IetfDraftTestVectorTests.cs
/// </summary>
public class SourceKnownEntityIdPostgresUuidCompatibilityTests
{
    [Theory]
    [DataInline]
    public async Task Postgres_Uuid_Column_Should_Store_And_Retrieve_Plain_And_Secure_Skeids_With_Full_Compatibility(DrnTestContext context)
    {
        // 1. Arrange configuration and identity services
        var (entityIdUtils, plainSkeid, secureSkeid) = Setup(context);

        // 2. Start PostgreSQL container and connect via Npgsql
        var container = await PostgresContext.StartAsync();
        var connectionString = container.GetConnectionString();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var conn = await dataSource.OpenConnectionAsync();

        // 3. Create table with native PostgreSQL UUID columns and indexes
        await using (var setupCmd = conn.CreateCommand())
        {
            setupCmd.CommandText = """
                                       DROP TABLE IF EXISTS skeid_uuid_compatibility_test;
                                       CREATE TABLE skeid_uuid_compatibility_test (
                                           id BIGINT PRIMARY KEY,
                                           plain_id UUID NOT NULL,
                                           secure_id UUID NOT NULL,
                                           nullable_id UUID NULL,
                                           format_description TEXT NOT NULL
                                       );
                                       CREATE INDEX idx_skeid_compat_plain ON skeid_uuid_compatibility_test(plain_id);
                                       CREATE INDEX idx_skeid_compat_secure ON skeid_uuid_compatibility_test(secure_id);
                                   """;
            await setupCmd.ExecuteNonQueryAsync();
        }

        // 4. Parameterized binary insert (.NET Guid -> PostgreSQL uuid)
        const long recordId1 = 1001L;
        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.CommandText = """
                                        INSERT INTO skeid_uuid_compatibility_test (id, plain_id, secure_id, nullable_id, format_description)
                                        VALUES (@id, @plain_id, @secure_id, @nullable_id, @desc);
                                    """;
            insertCmd.Parameters.AddWithValue("id", recordId1);
            insertCmd.Parameters.AddWithValue("plain_id", plainSkeid.EntityId);
            insertCmd.Parameters.AddWithValue("secure_id", secureSkeid.EntityId);
            insertCmd.Parameters.AddWithValue("nullable_id", DBNull.Value);
            insertCmd.Parameters.AddWithValue("desc", "Parameterized Binary Insert");
            await insertCmd.ExecuteNonQueryAsync();
        }

        // 5. String literal cast insert ('...'::uuid) in standard D (8-4-4-4-12) and N (32 hex) formats
        const long recordId2 = 1002L;
        await using (var castCmd = conn.CreateCommand())
        {
            castCmd.CommandText = $"""
                                       INSERT INTO skeid_uuid_compatibility_test (id, plain_id, secure_id, nullable_id, format_description)
                                       VALUES (
                                           {recordId2},
                                           '{plainSkeid.EntityId:D}'::uuid,
                                           '{secureSkeid.EntityId:D}'::uuid,
                                           '{plainSkeid.EntityId:N}'::uuid,
                                           'SQL String Literal Cast'
                                       );
                                   """;
            await castCmd.ExecuteNonQueryAsync();
        }

        // 6. Retrieve row 1 and verify bit-for-bit Guid identity and SKEID parsing
        await using (var queryCmd = conn.CreateCommand())
        {
            queryCmd.CommandText = """
                                       SELECT plain_id, secure_id, nullable_id
                                       FROM skeid_uuid_compatibility_test
                                       WHERE id = @id;
                                   """;
            queryCmd.Parameters.AddWithValue("id", recordId1);

            await using var reader = await queryCmd.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();

            var retrievedPlainGuid = reader.GetGuid(0);
            var retrievedSecureGuid = reader.GetGuid(1);
            var isNullableNull = await reader.IsDBNullAsync(2);

            // Assert bit-for-bit roundtrip
            retrievedPlainGuid.Should().Be(plainSkeid.EntityId);
            retrievedSecureGuid.Should().Be(secureSkeid.EntityId);
            isNullableNull.Should().BeTrue();

            entityIdUtils.ShouldBeEquivalentSkeid(retrievedPlainGuid, plainSkeid, SourceKnownEntityIdFormat.Plain);
            entityIdUtils.ShouldBeEquivalentSkeid(retrievedSecureGuid, secureSkeid, SourceKnownEntityIdFormat.Secure);
        }

        // 7. Retrieve row 2 (inserted via text cast) and verify bit-for-bit roundtrip and SKEID parsing
        await using (var queryCmd2 = conn.CreateCommand())
        {
            queryCmd2.CommandText = """
                                        SELECT plain_id, secure_id, nullable_id
                                        FROM skeid_uuid_compatibility_test
                                        WHERE id = @id;
                                    """;
            queryCmd2.Parameters.AddWithValue("id", recordId2);

            await using var reader = await queryCmd2.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();

            var retrievedPlainGuid2 = reader.GetGuid(0);
            var retrievedSecureGuid2 = reader.GetGuid(1);
            var retrievedNullableGuid2 = reader.GetGuid(2);

            retrievedPlainGuid2.Should().Be(plainSkeid.EntityId);
            retrievedSecureGuid2.Should().Be(secureSkeid.EntityId);
            retrievedNullableGuid2.Should().Be(plainSkeid.EntityId);

            entityIdUtils.ShouldBeEquivalentSkeid(retrievedPlainGuid2, plainSkeid, SourceKnownEntityIdFormat.Plain);
            entityIdUtils.ShouldBeEquivalentSkeid(retrievedSecureGuid2, secureSkeid, SourceKnownEntityIdFormat.Secure);
        }

        // 8. Test PostgreSQL query filtering by UUID parameter (indexed lookup)
        (await ExecuteScalarLongAsync(conn, "SELECT COUNT(*) FROM skeid_uuid_compatibility_test WHERE plain_id = @p;", "p", plainSkeid.EntityId))
            .Should().Be(2);

        (await ExecuteScalarLongAsync(conn, "SELECT COUNT(*) FROM skeid_uuid_compatibility_test WHERE secure_id = @s;", "s", secureSkeid.EntityId))
            .Should().Be(2);


        // 9. Test PostgreSQL ANY array filtering (batch lookup) with distinct IDs
        var nonExistentGuid = Guid.NewGuid();
        (await ExecuteScalarLongAsync(conn, "SELECT COUNT(*) FROM skeid_uuid_compatibility_test WHERE plain_id = ANY(@ids);", "ids", new[] { plainSkeid.EntityId, nonExistentGuid }))
            .Should().Be(2);

        // 10. Test batch inserts and roundtrip across 20 distinct IDs
        const int batchCount = 20;
        var batchRecords = new (long Id, SourceKnownEntityId Plain, SourceKnownEntityId Secure)[batchCount];
        for (var i = 0; i < batchCount; i++)
        {
            batchRecords[i] = (2000L + i, entityIdUtils.GeneratePlain<IetfDraftTestEntity>(), entityIdUtils.GenerateSecure<IetfDraftTestEntity>());
        }

        await using (var batchInsertCmd = conn.CreateCommand())
        {
            batchInsertCmd.CommandText = """
                                             INSERT INTO skeid_uuid_compatibility_test (id, plain_id, secure_id, nullable_id, format_description)
                                             VALUES (@id, @plain_id, @secure_id, NULL, 'Batch Record');
                                         """;
            var idParam = batchInsertCmd.Parameters.Add("id", NpgsqlTypes.NpgsqlDbType.Bigint);
            var plainParam = batchInsertCmd.Parameters.Add("plain_id", NpgsqlTypes.NpgsqlDbType.Uuid);
            var secureParam = batchInsertCmd.Parameters.Add("secure_id", NpgsqlTypes.NpgsqlDbType.Uuid);

            foreach (var record in batchRecords)
            {
                idParam.Value = record.Id;
                plainParam.Value = record.Plain.EntityId;
                secureParam.Value = record.Secure.EntityId;
                await batchInsertCmd.ExecuteNonQueryAsync();
            }
        }

        await using (var batchReadCmd = conn.CreateCommand())
        {
            batchReadCmd.CommandText = """
                                           SELECT id, plain_id, secure_id
                                           FROM skeid_uuid_compatibility_test
                                           WHERE id >= 2000 AND id < 2000 + @count
                                           ORDER BY id;
                                       """;
            batchReadCmd.Parameters.AddWithValue("count", batchCount);

            await using var reader = await batchReadCmd.ExecuteReaderAsync();
            var readIndex = 0;
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt64(0);
                var plainGuid = reader.GetGuid(1);
                var secureGuid = reader.GetGuid(2);

                var expected = batchRecords[readIndex];
                id.Should().Be(expected.Id);
                plainGuid.Should().Be(expected.Plain.EntityId);
                secureGuid.Should().Be(expected.Secure.EntityId);

                if (readIndex == 0)
                {
                    entityIdUtils.ShouldBeEquivalentSkeid(plainGuid, expected.Plain, SourceKnownEntityIdFormat.Plain);
                    entityIdUtils.ShouldBeEquivalentSkeid(secureGuid, expected.Secure, SourceKnownEntityIdFormat.Secure);
                }

                readIndex++;
            }

            readIndex.Should().Be(batchCount);
        }

        // 11. Test PostgreSQL ANY array filtering across distinct batch records
        var sampleBatchIds = new[] { batchRecords[1].Plain.EntityId, batchRecords[3].Plain.EntityId, batchRecords[7].Plain.EntityId };
        (await ExecuteScalarLongAsync(conn, "SELECT COUNT(*) FROM skeid_uuid_compatibility_test WHERE plain_id = ANY(@ids);", "ids", sampleBatchIds))
            .Should().Be(3);
    }

    private static async Task<long> ExecuteScalarLongAsync(NpgsqlConnection conn, string sql, string paramName, object paramValue)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue(paramName, paramValue);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
