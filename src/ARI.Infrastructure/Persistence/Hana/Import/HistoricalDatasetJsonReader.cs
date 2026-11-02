using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ARI.Core.Contracts;
using ARI.Core.Domain;

namespace ARI.Infrastructure.Persistence.Hana.Import;

public static class HistoricalDatasetJsonReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<HistoricalRepairDataset> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var manifestDto = root.GetProperty("manifest").Deserialize<ManifestDto>(SerializerOptions)
            ?? throw new JsonException("Dataset manifest is required.");
        var recordsElement = root.GetProperty("records");
        var actualChecksum = ComputeCanonicalChecksum(recordsElement);

        if (!string.Equals(actualChecksum, manifestDto.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Dataset checksum does not match its canonical record content.");
        }

        var records = recordsElement.Deserialize<List<RecordDto>>(SerializerOptions)
            ?? throw new JsonException("Dataset records are required.");
        var mapped = records.Select(Map).ToArray();
        return new HistoricalRepairDataset(
            new HistoricalDatasetManifest(
                manifestDto.DatasetId,
                manifestDto.GeneratorVersion,
                manifestDto.Seed,
                manifestDto.RecordCount,
                manifestDto.DataClassification,
                manifestDto.SchemaVersion,
                manifestDto.Checksum),
            mapped);
    }

    internal static string ComputeCanonicalChecksum(JsonElement records)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   buffer,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            WriteCanonical(writer, records);
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(
                             property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException($"Unsupported JSON value kind: {element.ValueKind}.");
        }
    }

    private static HistoricalRepairCase Map(RecordDto record)
    {
        var vehicle = new VehicleSnapshot(
            record.Vehicle.Id,
            record.Vehicle.Manufacturer,
            record.Vehicle.Model,
            record.Vehicle.ModelYear,
            record.Vehicle.MileageKm,
            record.Vehicle.CapturedAtUtc);
        var order = new RepairOrder(
            record.Order.Id,
            vehicle,
            record.Order.RepairCode,
            record.Order.ProblemDescription,
            record.Order.CreatedAtUtc,
            record.Order.KnownConditions);
        var operations = record.Operations.Select(operation => new RepairOperation(
            operation.Id,
            operation.RepairOrderId,
            operation.AssemblyCode,
            operation.OperationCode,
            operation.ReferenceLaborHours)).ToArray();
        var conditions = record.KnownConditions.Select(condition => new KnownRepairCondition(
            condition.ConditionCode,
            condition.Severity,
            condition.DocumentedAtUtc)).ToArray();
        var execution = new RepairExecution(
            record.Execution.Id,
            record.Execution.RepairOrderId,
            record.Execution.ActualLaborMinutes,
            record.Execution.StartedAtUtc,
            record.Execution.CompletedAtUtc,
            record.Execution.PerformedOperationCodes,
            record.Execution.RepairReport);
        var complications = record.Complications.Select(complication => new RepairComplication(
            complication.Id,
            complication.RepairExecutionId,
            complication.ComplicationCode,
            complication.AdditionalLaborMinutes,
            complication.OccurredAtUtc,
            complication.AdditionalPartCodes)).ToArray();
        var result = new HistoricalRepairCase(
            vehicle, order, operations, conditions, execution, complications);
        result.Validate();
        return result;
    }

    private sealed record ManifestDto(
        string DatasetId,
        string GeneratorVersion,
        int Seed,
        int RecordCount,
        string DataClassification,
        int SchemaVersion,
        string Checksum);

    private sealed record RecordDto(
        VehicleDto Vehicle,
        OrderDto Order,
        IReadOnlyList<OperationDto> Operations,
        IReadOnlyList<ConditionDto> KnownConditions,
        ExecutionDto Execution,
        IReadOnlyList<ComplicationDto> Complications);

    private sealed record VehicleDto(
        Guid Id,
        string Manufacturer,
        string Model,
        int ModelYear,
        int MileageKm,
        DateTimeOffset CapturedAtUtc);

    private sealed record OrderDto(
        Guid Id,
        string RepairCode,
        string? ProblemDescription,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<string> KnownConditions);

    private sealed record OperationDto(
        Guid Id,
        Guid RepairOrderId,
        string AssemblyCode,
        string OperationCode,
        decimal ReferenceLaborHours);

    private sealed record ConditionDto(
        string ConditionCode,
        string Severity,
        DateTimeOffset DocumentedAtUtc);

    private sealed record ExecutionDto(
        Guid Id,
        Guid RepairOrderId,
        int ActualLaborMinutes,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc,
        IReadOnlyList<string> PerformedOperationCodes,
        string RepairReport);

    private sealed record ComplicationDto(
        Guid Id,
        Guid RepairExecutionId,
        string ComplicationCode,
        int AdditionalLaborMinutes,
        DateTimeOffset OccurredAtUtc,
        IReadOnlyList<string> AdditionalPartCodes);
}
