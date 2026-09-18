using System.Text.Json;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Testing.Contexts.Postgres;
using Npgsql;
using NpgsqlTypes;
using static DRN.Test.Integration.Tests.Framework.Utils.SourceKnownIds.IetfTestData;

namespace DRN.Test.Integration.Tests.Framework.Utils.SourceKnownIds;

/// <summary>
/// Verifies that PostgreSQL native <c>json</c> and <c>jsonb</c> columns fully support both plaintext
/// <see cref="SourceKnownEntityId"/> (RFC 9562 UUIDv8) and AES-256 encrypted Secure
/// <see cref="SourceKnownEntityId"/>, ensuring that:
/// 1. Stored JSON/JSONB documents preserve bit-for-bit SKEID string representations and deserialize back accurately.
/// 2. Retrieved SKEID GUIDs parse back into <see cref="SourceKnownEntityId"/> with intact metadata.
/// 3. Extracted text cast to UUID (<c>(data->>'plainId')::uuid</c>) accepts both plain and secure SKEIDs.
/// 4. Case-sensitivity semantics of JSON string extraction vs. normalized UUID casts are verified.
/// 5. JSONB containment queries (<c>@></c>) match plain and secure SKEIDs.
/// 6. Query execution plans explicitly prove index uses (GIN and B-tree expression indexes) and table scans (Seq Scan).
/// 7. Batch collections across distinct generated IDs persist and filter correctly in JSON/JSONB.
/// Uses test data from <see cref="IetfTestData"/>.
/// </summary>
public class SourceKnownEntityIdPostgresJsonCompatibilityTests
{
    [Theory]
    [DataInline]
    public async Task Postgres_Json_And_Jsonb_Columns_Should_Store_Retrieve_And_Index_Plain_And_Secure_Skeids_With_Full_Compatibility(DrnTestContext context)
    {
        // 1. Arrange configuration and identity services
        var (entityIdUtils, plainSkeid, secureSkeid) = Setup(context);

        // 2. Start PostgreSQL container and connect via Npgsql
        var container = await PostgresContext.StartAsync();
        var connectionString = container.GetConnectionString();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var conn = await dataSource.OpenConnectionAsync();

        // 3. Create table with native JSON and JSONB columns, GIN index, and expression indexes
        await using (var setupCmd = conn.CreateCommand())
        {
            setupCmd.CommandText = """
                                       DROP TABLE IF EXISTS skeid_json_compatibility_test;
                                       CREATE TABLE skeid_json_compatibility_test (
                                           id BIGINT PRIMARY KEY,
                                           data_json JSON NOT NULL,
                                           data_jsonb JSONB NOT NULL,
                                           format_description TEXT NOT NULL
                                       );
                                       CREATE INDEX idx_skeid_jsonb_gin ON skeid_json_compatibility_test USING gin (data_jsonb);
                                       CREATE INDEX idx_skeid_jsonb_path_gin ON skeid_json_compatibility_test USING gin (data_jsonb jsonb_path_ops);
                                       CREATE INDEX idx_skeid_jsonb_plain_uuid ON skeid_json_compatibility_test (((data_jsonb->>'plainId')::uuid));
                                       CREATE INDEX idx_skeid_jsonb_secure_uuid ON skeid_json_compatibility_test (((data_jsonb->>'secureId')::uuid));
                                   """;
            await setupCmd.ExecuteNonQueryAsync();
        }

        // 4. Parameterized insert via Npgsql (NpgsqlDbType.Json and NpgsqlDbType.Jsonb)
        const long recordId1 = 1001L;
        var payload1 = new FormattedGuidPayload(plainSkeid.EntityId, secureSkeid.EntityId, "Parameterized JSON Insert");
        var payload1Json = JsonSerializer.Serialize(payload1, JsonConventions.DefaultOptions);

        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.CommandText = """
                                        INSERT INTO skeid_json_compatibility_test (id, data_json, data_jsonb, format_description)
                                        VALUES (@id, @data_json, @data_jsonb, @desc);
                                    """;
            insertCmd.Parameters.AddWithValue("id", recordId1);
            insertCmd.Parameters.Add("data_json", NpgsqlDbType.Json).Value = payload1Json;
            insertCmd.Parameters.Add("data_jsonb", NpgsqlDbType.Jsonb).Value = payload1Json;
            insertCmd.Parameters.AddWithValue("desc", "Parameterized Insert");
            await insertCmd.ExecuteNonQueryAsync();
        }

        // 5. String literal cast insert ('...'::json and '...'::jsonb)
        const long recordId2 = 1002L;
        await using (var castCmd = conn.CreateCommand())
        {
            castCmd.CommandText = $"""
                                       INSERT INTO skeid_json_compatibility_test (id, data_json, data_jsonb, format_description)
                                       VALUES (
                                           {recordId2},
                                           '{payload1Json}'::json,
                                           '{payload1Json}'::jsonb,
                                           'SQL String Literal Cast'
                                       );
                                   """;
            await castCmd.ExecuteNonQueryAsync();
        }

        // 6. Retrieve row 1 and verify roundtrip deserialization and SKEID parsing for both JSON and JSONB
        await using (var queryCmd = conn.CreateCommand())
        {
            queryCmd.CommandText = """
                                       SELECT data_json, data_jsonb
                                       FROM skeid_json_compatibility_test
                                       WHERE id = @id;
                                   """;
            queryCmd.Parameters.AddWithValue("id", recordId1);

            await using var reader = await queryCmd.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();

            var retrievedJsonStr = reader.GetString(0);
            var retrievedJsonbStr = reader.GetString(1);

            // Assert deserialization from JSON column
            var deserializedFromJson = JsonSerializer.Deserialize<FormattedGuidPayload>(retrievedJsonStr, JsonConventions.DefaultOptions);
            deserializedFromJson.Should().NotBeNull();
            deserializedFromJson.PlainId.Should().Be(plainSkeid.EntityId);
            deserializedFromJson.SecureId.Should().Be(secureSkeid.EntityId);

            entityIdUtils.ShouldBeEquivalentSkeid(deserializedFromJson.PlainId, plainSkeid, SourceKnownEntityIdFormat.Plain);
            entityIdUtils.ShouldBeEquivalentSkeid(deserializedFromJson.SecureId, secureSkeid, SourceKnownEntityIdFormat.Secure);

            // Assert deserialization from JSONB column
            var deserializedFromJsonb = JsonSerializer.Deserialize<FormattedGuidPayload>(retrievedJsonbStr, JsonConventions.DefaultOptions);
            deserializedFromJsonb.Should().NotBeNull();
            deserializedFromJsonb.PlainId.Should().Be(plainSkeid.EntityId);
            deserializedFromJsonb.SecureId.Should().Be(secureSkeid.EntityId);

            entityIdUtils.ShouldBeEquivalentSkeid(deserializedFromJsonb.PlainId, plainSkeid, SourceKnownEntityIdFormat.Plain);
            entityIdUtils.ShouldBeEquivalentSkeid(deserializedFromJsonb.SecureId, secureSkeid, SourceKnownEntityIdFormat.Secure);
        }

        // 7. Filter by extracted text cast to UUID ((data->>'...')::uuid = @id)
        (await ExecuteScalarLongAsync(conn, "SELECT id FROM skeid_json_compatibility_test WHERE (data_json->>'plainId')::uuid = @p;", "p", plainSkeid.EntityId))
            .Should().Be(recordId1);

        (await ExecuteScalarLongAsync(conn, "SELECT id FROM skeid_json_compatibility_test WHERE (data_json->>'secureId')::uuid = @s;", "s", secureSkeid.EntityId))
            .Should().Be(recordId1);

        (await ExecuteScalarLongAsync(conn, "SELECT id FROM skeid_json_compatibility_test WHERE (data_jsonb->>'plainId')::uuid = @p;", "p", plainSkeid.EntityId))
            .Should().Be(recordId1);

        (await ExecuteScalarLongAsync(conn, "SELECT id FROM skeid_json_compatibility_test WHERE (data_jsonb->>'secureId')::uuid = @s;", "s", secureSkeid.EntityId))
            .Should().Be(recordId1);

        // 8. Case sensitivity: raw text equality vs normalized UUID cast
        var uppercasePlainGuidStr = plainSkeid.EntityId.ToString("D").ToUpperInvariant();
        (await ExecuteScalarLongAsync(conn, "SELECT COUNT(*) FROM skeid_json_compatibility_test WHERE data_jsonb->>'plainId' = @rawText;", "rawText", uppercasePlainGuidStr))
            .Should().Be(0, "raw text extraction ->> performs case-sensitive comparison; uppercase string must not match lowercase JSON");

        (await ExecuteScalarLongAsync(conn, "SELECT COUNT(*) FROM skeid_json_compatibility_test WHERE (data_jsonb->>'plainId')::uuid = @uuidParam;", "uuidParam", Guid.Parse(uppercasePlainGuidStr)))
            .Should().Be(2, "casting extracted JSON text to ::uuid normalizes casing and matches all rows");

        // 9. Filter by JSONB containment (@>) for Plain and Secure SKEIDs
        var plainContainmentJson = $$"""{"plainId": "{{plainSkeid.EntityId:D}}"}""";
        (await ExecuteScalarLongAsync(conn, "SELECT COUNT(*) FROM skeid_json_compatibility_test WHERE data_jsonb @> @filter::jsonb;", "filter", plainContainmentJson, NpgsqlDbType.Jsonb))
            .Should().Be(2, "JSONB containment @> must match documents containing plain SKEID");

        var secureContainmentJson = $$"""{"secureId": "{{secureSkeid.EntityId:D}}"}""";
        (await ExecuteScalarLongAsync(conn, "SELECT COUNT(*) FROM skeid_json_compatibility_test WHERE data_jsonb @> @filter::jsonb;", "filter", secureContainmentJson, NpgsqlDbType.Jsonb))
            .Should().Be(2, "JSONB containment @> must match documents containing secure SKEID");

        // 10. Explicit verification of Index Uses vs Table Scans via EXPLAIN
        // A) Table Scan (Seq Scan) on data_json:
        // Native JSON type has no GIN or expression index support, requiring a sequential table scan.
        await using (var explainJsonSeqCmd = conn.CreateCommand())
        {
            explainJsonSeqCmd.CommandText = "SELECT id FROM skeid_json_compatibility_test WHERE (data_json->>'plainId')::uuid = @p;";
            explainJsonSeqCmd.Parameters.AddWithValue("p", plainSkeid.EntityId);
            var plan = await GetExplainPlanAsync(explainJsonSeqCmd);
            plan.Should().Contain("Seq Scan", "unindexed data_json column query must execute via table scan (Seq Scan)");
            plan.Should().Contain("skeid_json_compatibility_test");
        }

        // B) Table Scan (Seq Scan) on data_jsonb when index scans are disabled:
        await using (var forceSeqCmd = conn.CreateCommand())
        {
            forceSeqCmd.CommandText = "SET enable_seqscan = on; SET enable_indexscan = off; SET enable_bitmapscan = off;";
            await forceSeqCmd.ExecuteNonQueryAsync();
        }

        await using (var explainForcedSeqCmd = conn.CreateCommand())
        {
            explainForcedSeqCmd.CommandText = "SELECT id FROM skeid_json_compatibility_test WHERE data_jsonb @> @filter::jsonb;";
            explainForcedSeqCmd.Parameters.Add("filter", NpgsqlDbType.Jsonb).Value = plainContainmentJson;
            var plan = await GetExplainPlanAsync(explainForcedSeqCmd);
            plan.Should().Contain("Seq Scan", "forced sequential scan must report Seq Scan on skeid_json_compatibility_test");
            plan.Should().Contain("skeid_json_compatibility_test");
        }

        // C) Index Scan on data_jsonb via GIN index:
        // Disable seqscan in session so planner prefers index scan regardless of table row count.
        await using (var forceIndexCmd = conn.CreateCommand())
        {
            forceIndexCmd.CommandText = "SET enable_seqscan = off; SET enable_indexscan = on; SET enable_bitmapscan = on;";
            await forceIndexCmd.ExecuteNonQueryAsync();
        }

        await using (var explainGinCmd = conn.CreateCommand())
        {
            explainGinCmd.CommandText = "SELECT id FROM skeid_json_compatibility_test WHERE data_jsonb @> @filter::jsonb;";
            explainGinCmd.Parameters.Add("filter", NpgsqlDbType.Jsonb).Value = plainContainmentJson;
            var plan = await GetExplainPlanAsync(explainGinCmd);
            plan.Should().ContainAny(["Bitmap Index Scan", "Index Scan"], "JSONB containment query must utilize GIN index");
            plan.Should().ContainAny(["idx_skeid_jsonb_gin", "idx_skeid_jsonb_path_gin"]);
        }

        // D) Expression Index Scan on ((data_jsonb->>'plainId')::uuid):
        await using (var explainExprCmd = conn.CreateCommand())
        {
            explainExprCmd.CommandText = "SELECT id FROM skeid_json_compatibility_test WHERE (data_jsonb->>'plainId')::uuid = @p;";
            explainExprCmd.Parameters.AddWithValue("p", plainSkeid.EntityId);
            var plan = await GetExplainPlanAsync(explainExprCmd);
            plan.Should().ContainAny(["Bitmap Index Scan", "Index Scan"], "cast expression query must utilize B-tree expression index");
            plan.Should().Contain("idx_skeid_jsonb_plain_uuid");
        }

        // E) Expression Index Scan on ((data_jsonb->>'secureId')::uuid):
        await using (var explainSecureExprCmd = conn.CreateCommand())
        {
            explainSecureExprCmd.CommandText = "SELECT id FROM skeid_json_compatibility_test WHERE (data_jsonb->>'secureId')::uuid = @s;";
            explainSecureExprCmd.Parameters.AddWithValue("s", secureSkeid.EntityId);
            var plan = await GetExplainPlanAsync(explainSecureExprCmd);
            plan.Should().ContainAny(["Bitmap Index Scan", "Index Scan"], "secure SKEID cast expression query must utilize B-tree expression index");
            plan.Should().Contain("idx_skeid_jsonb_secure_uuid");
        }

        // Reset planner flags for remaining tests
        await using (var resetFlagsCmd = conn.CreateCommand())
        {
            resetFlagsCmd.CommandText = "RESET enable_seqscan; RESET enable_indexscan; RESET enable_bitmapscan;";
            await resetFlagsCmd.ExecuteNonQueryAsync();
        }

        // 11. Batch inserts and ordered retrieval across 20 distinct IDs
        const int batchCount = 20;
        var batchRecords = new (long Id, SourceKnownEntityId Plain, SourceKnownEntityId Secure)[batchCount];
        for (var i = 0; i < batchCount; i++)
        {
            batchRecords[i] = (2000L + i, entityIdUtils.GeneratePlain<IetfDraftTestEntity>(), entityIdUtils.GenerateSecure<IetfDraftTestEntity>());
        }

        await using (var batchInsertCmd = conn.CreateCommand())
        {
            batchInsertCmd.CommandText = """
                                             INSERT INTO skeid_json_compatibility_test (id, data_json, data_jsonb, format_description)
                                             VALUES (@id, @data_json, @data_jsonb, 'Batch Record');
                                         """;
            var idParam = batchInsertCmd.Parameters.Add("id", NpgsqlDbType.Bigint);
            var jsonParam = batchInsertCmd.Parameters.Add("data_json", NpgsqlDbType.Json);
            var jsonbParam = batchInsertCmd.Parameters.Add("data_jsonb", NpgsqlDbType.Jsonb);

            foreach (var record in batchRecords)
            {
                var batchPayload = new FormattedGuidPayload(record.Plain.EntityId, record.Secure.EntityId, $"Batch_{record.Id}");
                var batchJson = JsonSerializer.Serialize(batchPayload, JsonConventions.DefaultOptions);

                idParam.Value = record.Id;
                jsonParam.Value = batchJson;
                jsonbParam.Value = batchJson;
                await batchInsertCmd.ExecuteNonQueryAsync();
            }
        }

        await using (var batchReadCmd = conn.CreateCommand())
        {
            batchReadCmd.CommandText = """
                                           SELECT id, data_json, data_jsonb
                                           FROM skeid_json_compatibility_test
                                           WHERE id >= 2000 AND id < 2000 + @count
                                           ORDER BY id;
                                       """;
            batchReadCmd.Parameters.AddWithValue("count", batchCount);

            await using var reader = await batchReadCmd.ExecuteReaderAsync();
            var readIndex = 0;
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt64(0);
                var jsonStr = reader.GetString(1);
                var jsonbStr = reader.GetString(2);

                var expected = batchRecords[readIndex];
                id.Should().Be(expected.Id);

                var parsedFromJson = JsonSerializer.Deserialize<FormattedGuidPayload>(jsonStr, JsonConventions.DefaultOptions);
                parsedFromJson!.PlainId.Should().Be(expected.Plain.EntityId);
                parsedFromJson.SecureId.Should().Be(expected.Secure.EntityId);

                var parsedFromJsonb = JsonSerializer.Deserialize<FormattedGuidPayload>(jsonbStr, JsonConventions.DefaultOptions);
                parsedFromJsonb!.PlainId.Should().Be(expected.Plain.EntityId);
                parsedFromJsonb.SecureId.Should().Be(expected.Secure.EntityId);

                if (readIndex == 0)
                {
                    entityIdUtils.ShouldBeEquivalentSkeid(parsedFromJson.PlainId, expected.Plain, SourceKnownEntityIdFormat.Plain);
                    entityIdUtils.ShouldBeEquivalentSkeid(parsedFromJson.SecureId, expected.Secure, SourceKnownEntityIdFormat.Secure);
                    entityIdUtils.ShouldBeEquivalentSkeid(parsedFromJsonb.PlainId, expected.Plain, SourceKnownEntityIdFormat.Plain);
                    entityIdUtils.ShouldBeEquivalentSkeid(parsedFromJsonb.SecureId, expected.Secure, SourceKnownEntityIdFormat.Secure);
                }

                readIndex++;
            }

            readIndex.Should().Be(batchCount);
        }

        // 12. Containment query across batch items to verify index lookup reliability
        var sampleBatchRecord = batchRecords[10];
        var sampleFilter = $$"""{"plainId": "{{sampleBatchRecord.Plain.EntityId:D}}"}""";
        (await ExecuteScalarLongAsync(conn, "SELECT id FROM skeid_json_compatibility_test WHERE data_jsonb @> @filter::jsonb;", "filter", sampleFilter, NpgsqlDbType.Jsonb))
            .Should().Be(sampleBatchRecord.Id, "JSONB containment lookup must find the exact batch record by plain SKEID");
    }

    private static async Task<long> ExecuteScalarLongAsync(
        NpgsqlConnection conn,
        string sql,
        string paramName,
        object paramValue,
        NpgsqlDbType? dbType = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var param = dbType.HasValue ? cmd.Parameters.Add(paramName, dbType.Value) : cmd.Parameters.AddWithValue(paramName, paramValue);
        if (dbType.HasValue) param.Value = paramValue;
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<string> GetExplainPlanAsync(NpgsqlCommand cmd)
    {
        cmd.CommandText = $"EXPLAIN {cmd.CommandText}";
        var lines = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                lines.Add(reader.GetString(0));
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
