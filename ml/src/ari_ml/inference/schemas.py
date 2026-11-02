"""HTTP schemas aligned with ARI.Core MachineLearningContracts."""

from __future__ import annotations

from pydantic import BaseModel, ConfigDict, Field, field_validator


class PredictionRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    repair_code: str = Field(alias="repairCode", min_length=1, max_length=100)
    manufacturer: str = Field(min_length=1, max_length=100)
    model: str = Field(min_length=1, max_length=100)
    vehicle_age_years: int = Field(alias="vehicleAgeYears", ge=0, le=100)
    mileage_km: int = Field(alias="mileageKm", ge=0, le=1_000_000)
    baseline_labor_hours: float = Field(alias="baselineLaborHours", gt=0, le=100)
    known_conditions: list[str] = Field(alias="knownConditions", default_factory=list)
    extracted_conditions: list[str] = Field(
        alias="extractedConditions", default_factory=list
    )

    @field_validator("repair_code")
    @classmethod
    def normalize_repair_code(cls, value: str) -> str:
        return value.strip().upper()


class PredictionResponse(BaseModel):
    model_config = ConfigDict(populate_by_name=True, serialize_by_alias=True)

    predicted_labor_hours: float = Field(alias="predictedLaborHours")
    model_version: str = Field(alias="modelVersion")
    data_source: str = Field(alias="dataSource")
    range_lower_hours: float | None = Field(alias="rangeLowerHours", default=None)
    range_upper_hours: float | None = Field(alias="rangeUpperHours", default=None)
    prediction_method: str = Field(alias="predictionMethod")
    status: str


class ReadinessResponse(BaseModel):
    model_config = ConfigDict(populate_by_name=True, serialize_by_alias=True)

    status: str
    model_version: str | None = Field(alias="modelVersion", default=None)
    feature_schema_version: str | None = Field(
        alias="featureSchemaVersion", default=None
    )
