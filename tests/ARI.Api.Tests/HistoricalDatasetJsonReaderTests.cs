using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ARI.Infrastructure.Persistence.Hana.Import;

namespace ARI.Api.Tests;

public sealed class HistoricalDatasetJsonReaderTests
{
    [Fact]
    public void Canonical_checksum_is_independent_of_object_property_order()
    {
        using var first = JsonDocument.Parse("[{\"b\":2,\"a\":1}]");
        using var second = JsonDocument.Parse("[{\"a\":1,\"b\":2}]");
        var expected = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes("[{\"a\":1,\"b\":2}]")));

        Assert.Equal(expected, HistoricalDatasetJsonReader.ComputeCanonicalChecksum(first.RootElement));
        Assert.Equal(expected, HistoricalDatasetJsonReader.ComputeCanonicalChecksum(second.RootElement));
    }

    [Fact]
    public async Task Mismatched_checksum_is_rejected_before_mapping_or_database_access()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(
                path,
                """
                {
                  "manifest": {
                    "datasetId": "bad", "generatorVersion": "1.0.0", "seed": 1,
                    "recordCount": 0, "dataClassification": "SYNTHETIC",
                    "schemaVersion": 2, "checksum": "not-the-real-checksum"
                  },
                  "records": []
                }
                """);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => HistoricalDatasetJsonReader.ReadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
