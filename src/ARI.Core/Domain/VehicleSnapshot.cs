namespace ARI.Core.Domain;

/// <summary>Vehicle facts as they were known at the time an estimate was requested.</summary>
public sealed record VehicleSnapshot(
    Guid Id,
    string Manufacturer,
    string Model,
    int ModelYear,
    int MileageKm,
    DateTimeOffset CapturedAtUtc);
