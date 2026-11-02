"""Versioned feature schema shared by training and inference."""

from __future__ import annotations

import pandas as pd

FEATURE_SCHEMA_VERSION = "ari-labor-features-v1"
NUMERIC_FEATURES = (
    "vehicle_age_years",
    "mileage_km",
    "baseline_labor_minutes",
    "known_corrosion",
)
CATEGORICAL_FEATURES = ("repair_code", "manufacturer", "model")
FEATURE_COLUMNS = NUMERIC_FEATURES + CATEGORICAL_FEATURES
TARGET_COLUMN = "actual_labor_minutes"

FORBIDDEN_FEATURES = (
    "actual_labor_minutes",
    "complication_codes",
    "repair_report",
    "completed_at_utc",
    "repair_order_id",
    "repair_operation_id",
)


def assert_feature_schema_safe(feature_names: tuple[str, ...] = FEATURE_COLUMNS) -> None:
    leaked = sorted(set(feature_names).intersection(FORBIDDEN_FEATURES))
    if leaked:
        raise ValueError(f"post-start or identity fields leaked into features: {', '.join(leaked)}")
    if tuple(feature_names) != FEATURE_COLUMNS:
        raise ValueError("feature order does not match the versioned schema")


def build_training_frame(records: list[dict]) -> pd.DataFrame:
    """Map transport fields to explicit snake_case ML columns.

    Outcome-only columns remain available for audits and segmented evaluation, but callers select
    only FEATURE_COLUMNS before passing data to a scikit-learn pipeline.
    """

    rows = []
    for record in records:
        rows.append(
            {
                "repair_order_id": record["repairOrderId"],
                "repair_operation_id": record["repairOperationId"],
                "created_at_utc": pd.Timestamp(record["createdAtUtc"]),
                "completed_at_utc": pd.Timestamp(record["completedAtUtc"]),
                "vehicle_age_years": int(record["vehicleAgeYears"]),
                "mileage_km": int(record["mileageKm"]),
                "manufacturer": str(record["manufacturer"]),
                "model": str(record["model"]),
                "repair_code": str(record["repairCode"]),
                "baseline_labor_minutes": float(record["baselineLaborMinutes"]),
                "known_corrosion": int(bool(record["knownCorrosion"])),
                "actual_labor_minutes": float(record["actualLaborMinutes"]),
                "complication_codes": tuple(record["complicationCodes"]),
                "repair_report": str(record["repairReport"]),
            }
        )
    frame = pd.DataFrame(rows)
    _validate_frame(frame)
    return frame


def _validate_frame(frame: pd.DataFrame) -> None:
    assert_feature_schema_safe()
    if frame.empty:
        raise ValueError("training export contains no rows")
    if frame["repair_order_id"].duplicated().any():
        raise ValueError("ARI-ML-01 expects one completed operation per repair order")
    if (frame["vehicle_age_years"] < 0).any():
        raise ValueError("vehicle age must not be negative")
    if ((frame["mileage_km"] < 0) | (frame["mileage_km"] > 1_000_000)).any():
        raise ValueError("mileage is outside the supported range")
    if (frame["baseline_labor_minutes"] <= 0).any():
        raise ValueError("baseline labor must be positive")
    if (frame[TARGET_COLUMN] <= 0).any():
        raise ValueError("actual labor target must be positive")
    if (frame["created_at_utc"] > frame["completed_at_utc"]).any():
        raise ValueError("a repair cannot complete before its order was created")
