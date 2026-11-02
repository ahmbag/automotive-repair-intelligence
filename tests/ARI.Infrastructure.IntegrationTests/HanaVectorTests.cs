using System.Data;
using ARI.Core.Contracts;
using ARI.Infrastructure.AI.Embeddings;
using ARI.Infrastructure.Persistence.Hana;
using ARI.Infrastructure.Persistence.Hana.Vector;
using Xunit.Sdk;

namespace ARI.Infrastructure.IntegrationTests;

[Trait("Category", "HanaIntegration")]
public sealed class HanaVectorTests
{
    [Fact]
    public async Task Vector_engine_smoke_functions_return_expected_values()
    {
        var context = CreateContextOrSkip();
        await using var connection = await context.Factory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COSINE_SIMILARITY(TO_REAL_VECTOR('[1,0,0]'), TO_REAL_VECTOR('[1,0,0]')),
                   CARDINALITY(TO_REAL_VECTOR('[0.1,0.2,0.3]'))
            FROM DUMMY
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1d, Convert.ToDouble(reader.GetValue(0)), 6);
        Assert.Equal(3, reader.GetInt32(1));
    }

    [Fact]
    public async Task App_user_can_store_and_semantically_retrieve_a_vector_with_real_source_join()
    {
        var context = CreateContextOrSkip();
        await using var connection = await context.Factory.OpenConnectionAsync();
        Guid orderId;
        string repairCode;
        DateTime availableAt;
        await using (var source = connection.CreateCommand())
        {
            source.CommandText = """
                SELECT TOP 1 RO.ID, RO.REPAIR_CODE, RX.COMPLETED_AT_UTC
                FROM ARI.REPAIR_ORDERS RO
                INNER JOIN ARI.REPAIR_EXECUTIONS RX ON RX.REPAIR_ORDER_ID = RO.ID
                WHERE RO.DATASET_ID = 'ari-suspension-synthetic-v1'
                ORDER BY RO.ID
                """;
            await using var reader = await source.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            orderId = Guid.Parse(reader.GetString(0));
            repairCode = reader.GetString(1);
            availableAt = reader.GetDateTime(2);
        }

        var vector = new float[1536];
        vector[0] = 1;
        var options = new EmbeddingProviderOptions
        {
            Model = "ari-integration-test-embedding",
            ModelVersion = "v1",
            Dimensions = 1536
        };
        var repository = new HanaVectorRepository(context.Factory, context.Options);
        var id = Guid.NewGuid();
        try
        {
            await repository.StoreAsync([new RepairEmbeddingRecord(
                id,
                orderId,
                0,
                vector,
                options.Model,
                options.ModelVersion,
                new string('a', 64),
                new DateTimeOffset(DateTime.SpecifyKind(availableAt, DateTimeKind.Utc))) ]);
            var result = await repository.SearchAsync(
                new RepairRetrievalQuery(
                    "integration vector", repairCode, null, null, null, null, [],
                    DateTimeOffset.UtcNow.AddDays(1), RepairRetrievalStrategies.Semantic, 1, 0.99),
                vector,
                options);

            var match = Assert.Single(result);
            Assert.Equal(orderId, match.RepairOrderId);
            Assert.True(match.VectorSimilarity >= 0.99);
            Assert.StartsWith("repair-order:", match.SourceReference);
            Assert.False(string.IsNullOrWhiteSpace(match.SourceExcerpt));
        }
        finally
        {
            await using var cleanupConnection = await context.Factory.OpenConnectionAsync();
            await using var cleanup = cleanupConnection.CreateCommand();
            cleanup.CommandText = "DELETE FROM ARI.REPAIR_REPORT_EMBEDDINGS WHERE ID = ?";
            var parameter = cleanup.CreateParameter();
            parameter.ParameterName = "p0";
            parameter.DbType = DbType.String;
            parameter.Value = id.ToString("D");
            cleanup.Parameters.Add(parameter);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Production_embedding_index_contains_exactly_all_historical_reports()
    {
        if (Environment.GetEnvironmentVariable("ARI_EXPECT_RAG_INDEX") != "1")
        {
            throw SkipException.ForSkip("Set ARI_EXPECT_RAG_INDEX=1 after running the RAG indexer.");
        }

        var context = CreateContextOrSkip();
        await using var connection = await context.Factory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*), COUNT(DISTINCT REPAIR_ORDER_ID),
                   MIN(CARDINALITY(EMBEDDING)), MAX(CARDINALITY(EMBEDDING))
            FROM ARI.REPAIR_REPORT_EMBEDDINGS
            WHERE EMBEDDING_MODEL = 'text-embedding-3-small'
              AND MODEL_VERSION = 'repair-report-embedding-v1'
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1000, reader.GetInt32(0));
        Assert.Equal(1000, reader.GetInt32(1));
        Assert.Equal(1536, reader.GetInt32(2));
        Assert.Equal(1536, reader.GetInt32(3));
    }

    [Fact]
    public async Task Production_v2_index_contains_two_separated_profiles_for_every_case()
    {
        if (Environment.GetEnvironmentVariable("ARI_EXPECT_RAG_V2_INDEX") != "1")
            throw SkipException.ForSkip("Set ARI_EXPECT_RAG_V2_INDEX=1 after running the V2 indexer.");

        var context = CreateContextOrSkip();
        await using var connection = await context.Factory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*), COUNT(DISTINCT REPAIR_ORDER_ID), COUNT(DISTINCT PROFILE_TYPE),
                   MIN(CARDINALITY(EMBEDDING)), MAX(CARDINALITY(EMBEDDING))
            FROM ARI.REPAIR_SEARCH_EMBEDDINGS_V2
            WHERE EMBEDDING_MODEL = 'text-embedding-3-small'
              AND MODEL_VERSION = 'repair-search-embedding-v2'
              AND PROFILE_VERSION = 'canonical-repair-search-profile-v2'
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2000, reader.GetInt32(0));
        Assert.Equal(1000, reader.GetInt32(1));
        Assert.Equal(2, reader.GetInt32(2));
        Assert.Equal(1536, reader.GetInt32(3));
        Assert.Equal(1536, reader.GetInt32(4));
    }

    [Fact]
    public async Task V2_structured_candidates_respect_historical_availability()
    {
        var context = CreateContextOrSkip();
        var repository = new HanaRepairSearchV2Repository(context.Factory, context.Options);
        var options = new EmbeddingProviderOptions
        {
            Model = "text-embedding-3-small", V2ModelVersion = "repair-search-embedding-v2",
            V2ProfileVersion = "canonical-repair-search-profile-v2", V2DocumentVersion = "2.0.0"
        };
        var result = await repository.SearchAsync(new RepairRetrievalQuery(
            "planned control arm", "CONTROL_ARM_FRONT_LEFT_REPLACE", null, null, null, null, [],
            new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
            RepairRetrievalStrategies.Structured, 5, -1, RepairEmbeddingProfiles.RepairProfileV2),
            null, options);
        Assert.Empty(result);
    }

    private static TestContext CreateContextOrSkip()
    {
        var host = Environment.GetEnvironmentVariable("Hana__Host");
        var user = Environment.GetEnvironmentVariable("Hana__User");
        var password = Environment.GetEnvironmentVariable("Hana__Password");
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw SkipException.ForSkip("SAP HANA integration configuration is absent.");
        }

        var options = new HanaOptions
        {
            Host = host,
            Port = int.TryParse(Environment.GetEnvironmentVariable("Hana__Port"), out var port) ? port : 443,
            User = user,
            Password = password,
            ConnectionTimeoutSeconds = 60
        };
        return new TestContext(new HanaConnectionFactory(options), options);
    }

    private sealed record TestContext(HanaConnectionFactory Factory, HanaOptions Options);
}
