using System.Text;
using System.Text.Json;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Testing.Contexts.Postgres;
using Npgsql;
using NpgsqlTypes;

namespace DRN.Test.Integration.Tests.Framework.Utils.SourceKnownIds;

/// <summary>
/// Payload record for testing forged GUIDs across all 16 UUID version nibbles and all 16 variant high nibbles.
/// </summary>
public record ForgedGuidBatchItem(
    int Id,
    int Version,
    int VariantNibble,
    Guid ForgedGuid,
    string VariantFamily,
    string Description);

/// <summary>
/// Verifies batch compatibility across all 16 UUID versions (0 to 15, hex 0x0 to 0xF) and all variant
/// families (Apollo NCS, RFC 9562/4122, Microsoft COM, and Reserved) for:
/// 1. In-memory .NET Guid parsing and <see cref="Guid.Version"/> fidelity.
/// 2. System.Text.Json batch serialization and deserialization (all together).
/// 3. PostgreSQL native <c>uuid</c> column with one batch insert and one select-all query.
/// 4. PostgreSQL native <c>json</c> and <c>jsonb</c> columns with batch document storage and SQL recordset deserialization.
/// </summary>
public class UuidVersionAndVariantBatchCompatibilityTests
{
    [Fact]
    public async Task All_16_Uuid_Versions_Should_Be_Supported_By_Json_And_Postgres()
    {
        // 1. Generate full matrix of 256 forged GUIDs (16 versions x 16 variant high nibbles)
        var items = GenerateBatch();
        items.Should().HaveCount(256);

        // Verify in-memory .NET Guid properties for all 256 items
        foreach (var item in items)
        {
            item.ForgedGuid.Version.Should().Be(item.Version, $"Guid.Version must match the forged version nibble {item.Version}");
        }

        // 2. System.Text.Json batch serialization and deserialization (all together)
        var batchJson = JsonSerializer.Serialize(items, JsonConventions.DefaultOptions);
        var deserializedBatch = JsonSerializer.Deserialize<List<ForgedGuidBatchItem>>(batchJson, JsonConventions.DefaultOptions);

        deserializedBatch.Should().NotBeNull();
        deserializedBatch.Should().HaveCount(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var expected = items[i];
            var actual = deserializedBatch[i];
            actual.Id.Should().Be(expected.Id);
            actual.Version.Should().Be(expected.Version);
            actual.VariantNibble.Should().Be(expected.VariantNibble);
            actual.ForgedGuid.Should().Be(expected.ForgedGuid);
            actual.ForgedGuid.Version.Should().Be(expected.Version);
            actual.VariantFamily.Should().Be(expected.VariantFamily);
            actual.Description.Should().Be(expected.Description);
        }

        // 3. PostgreSQL setup via testcontainer
        var container = await PostgresContext.StartAsync();
        var connectionString = container.GetConnectionString();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var conn = await dataSource.OpenConnectionAsync();

        // 4. Create table for native PostgreSQL UUID batch testing
        await using (var setupCmd = conn.CreateCommand())
        {
            setupCmd.CommandText = """
                DROP TABLE IF EXISTS forged_uuid_batch_test;
                CREATE TABLE forged_uuid_batch_test (
                    id INT PRIMARY KEY,
                    version_num INT NOT NULL,
                    variant_nibble INT NOT NULL,
                    forged_guid UUID NOT NULL,
                    variant_family TEXT NOT NULL,
                    description TEXT NOT NULL
                );
                CREATE INDEX idx_forged_uuid_batch_guid ON forged_uuid_batch_test (forged_guid);
            """;
            await setupCmd.ExecuteNonQueryAsync();
        }

        // 5. ONE INSERT: Insert all 256 items in a single multi-row parameterized command
        await using (var insertCmd = conn.CreateCommand())
        {
            var sqlBuilder = new StringBuilder(
                "INSERT INTO forged_uuid_batch_test (id, version_num, variant_nibble, forged_guid, variant_family, description) VALUES ");

            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) sqlBuilder.Append(", ");
                sqlBuilder.Append($"(@id{i}, @ver{i}, @var{i}, @guid{i}, @fam{i}, @desc{i})");
                insertCmd.Parameters.AddWithValue($"id{i}", items[i].Id);
                insertCmd.Parameters.AddWithValue($"ver{i}", items[i].Version);
                insertCmd.Parameters.AddWithValue($"var{i}", items[i].VariantNibble);
                insertCmd.Parameters.AddWithValue($"guid{i}", items[i].ForgedGuid);
                insertCmd.Parameters.AddWithValue($"fam{i}", items[i].VariantFamily);
                insertCmd.Parameters.AddWithValue($"desc{i}", items[i].Description);
            }
            sqlBuilder.Append(';');
            insertCmd.CommandText = sqlBuilder.ToString();
            await insertCmd.ExecuteNonQueryAsync();
        }

        // 6. ONE SELECT ALL: Retrieve all 256 rows in a single query
        var retrievedList = new List<ForgedGuidBatchItem>(items.Count);
        await using (var selectCmd = conn.CreateCommand())
        {
            selectCmd.CommandText = """
                SELECT id, version_num, variant_nibble, forged_guid, variant_family, description
                FROM forged_uuid_batch_test
                ORDER BY id;
            """;
            await using var reader = await selectCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                retrievedList.Add(new ForgedGuidBatchItem(
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2),
                    reader.GetGuid(3),
                    reader.GetString(4),
                    reader.GetString(5)));
            }
        }

        // 7. Verify selects are identical to inserted ones
        retrievedList.Should().HaveCount(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var expected = items[i];
            var actual = retrievedList[i];
            actual.Id.Should().Be(expected.Id);
            actual.Version.Should().Be(expected.Version);
            actual.VariantNibble.Should().Be(expected.VariantNibble);
            actual.ForgedGuid.Should().Be(expected.ForgedGuid);
            actual.ForgedGuid.Version.Should().Be(expected.Version);
            actual.VariantFamily.Should().Be(expected.VariantFamily);
            actual.Description.Should().Be(expected.Description);
        }

        // 8. PostgreSQL JSON and JSONB batch testing: one insert and one select all
        await using (var jsonSetupCmd = conn.CreateCommand())
        {
            jsonSetupCmd.CommandText = """
                DROP TABLE IF EXISTS forged_json_batch_test;
                CREATE TABLE forged_json_batch_test (
                    id INT PRIMARY KEY,
                    data_json JSON NOT NULL,
                    data_jsonb JSONB NOT NULL
                );
            """;
            await jsonSetupCmd.ExecuteNonQueryAsync();
        }

        // ONE JSON INSERT: Store serialized 256-item JSON document into json and jsonb columns
        await using (var jsonInsertCmd = conn.CreateCommand())
        {
            jsonInsertCmd.CommandText = """
                INSERT INTO forged_json_batch_test (id, data_json, data_jsonb)
                VALUES (1, @data_json, @data_jsonb);
            """;
            jsonInsertCmd.Parameters.Add("data_json", NpgsqlDbType.Json).Value = batchJson;
            jsonInsertCmd.Parameters.Add("data_jsonb", NpgsqlDbType.Jsonb).Value = batchJson;
            await jsonInsertCmd.ExecuteNonQueryAsync();
        }

        // ONE JSON SELECT ALL: Retrieve the documents back and verify
        await using (var jsonSelectCmd = conn.CreateCommand())
        {
            jsonSelectCmd.CommandText = "SELECT data_json, data_jsonb FROM forged_json_batch_test WHERE id = 1;";
            await using var jsonReader = await jsonSelectCmd.ExecuteReaderAsync();
            (await jsonReader.ReadAsync()).Should().BeTrue();

            var retrievedJson = jsonReader.GetString(0);
            var retrievedJsonb = jsonReader.GetString(1);

            var deserializedFromJson = JsonSerializer.Deserialize<List<ForgedGuidBatchItem>>(retrievedJson, JsonConventions.DefaultOptions);
            var deserializedFromJsonb = JsonSerializer.Deserialize<List<ForgedGuidBatchItem>>(retrievedJsonb, JsonConventions.DefaultOptions);

            deserializedFromJson.Should().NotBeNull();
            deserializedFromJson.Should().HaveCount(items.Count);
            deserializedFromJsonb.Should().NotBeNull();
            deserializedFromJsonb.Should().HaveCount(items.Count);

            for (var i = 0; i < items.Count; i++)
            {
                var expected = items[i];
                deserializedFromJson[i].Should().Be(expected);
                deserializedFromJsonb[i].Should().Be(expected);
            }
        }

        // ONE SQL RECORDSET QUERY: Validate PostgreSQL's internal JSON-to-UUID casting for all 256 items
        await using (var jsonRecordsetCmd = conn.CreateCommand())
        {
            jsonRecordsetCmd.CommandText = """
                SELECT count(*)
                FROM jsonb_to_recordset(
                    (SELECT data_jsonb FROM forged_json_batch_test WHERE id = 1)
                ) AS x(id INT, version_num INT, variant_nibble INT, forged_guid UUID, variant_family TEXT, description TEXT);
            """;
            var count = (long)(await jsonRecordsetCmd.ExecuteScalarAsync())!;
            count.Should().Be(items.Count);
        }
    }

    #region Helper Methods

    /// <summary>
    /// Forges 256 distinct GUIDs covering all 16 versions (0 to 15) across all 16 variant high nibbles (0 to 15).
    /// </summary>
    private static List<ForgedGuidBatchItem> GenerateBatch()
    {
        var batch = new List<ForgedGuidBatchItem>(256);
        var id = 1;
        for (var version = 0; version < 16; version++)
        {
            for (var variantNibble = 0; variantNibble < 16; variantNibble++)
            {
                // Standard 36-character hyphenated UUID layout:
                // xxxxxxxx-xxxx-Vxxx-Rxxx-xxxxxxxxxxxx
                // Octet 6 high nibble is version (bits 48-51)
                // Octet 8 high nibble is variant (bits 64-67)
                var guidString = $"{id:d8}-a1b2-{version:x}c3d-{variantNibble:x}e4f-0123456789ab";
                var guid = Guid.Parse(guidString);
                var family = GetVariantFamily(variantNibble);
                var description = $"v{version}_var0x{variantNibble:x}_{family}";

                batch.Add(new ForgedGuidBatchItem(id++, version, variantNibble, guid, family, description));
            }
        }

        return batch;
    }

    /// <summary>
    /// Classifies the variant nibble into RFC 9562/4122 variant families.
    /// </summary>
    private static string GetVariantFamily(int variantNibble) => variantNibble switch
    {
        <= 7 => "NCS",
        <= 11 => "RFC9562",
        12 or 13 => "MicrosoftCOM",
        _ => "Reserved"
    };

    #endregion
}
