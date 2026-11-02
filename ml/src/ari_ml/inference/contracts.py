"""Provider-neutral inference messages matching the .NET integration boundary."""

from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class LaborPredictionInput:
    repair_code: str
    manufacturer: str
    model: str
    vehicle_age_years: int
    mileage_km: int
    baseline_labor_hours: float
    known_conditions: tuple[str, ...]
    extracted_conditions: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class LaborPredictionResult:
    predicted_labor_hours: float
    model_version: str
    data_source: str
    range_lower_hours: float | None = None
    range_upper_hours: float | None = None
    prediction_method: str = "ML_REGRESSION"
    status: str = "PREDICTED"
