"""Trusted-artifact loader and deterministic single-row inference."""

from __future__ import annotations

import hashlib
import json
import math
from importlib.metadata import version
from pathlib import Path

import joblib
import pandas as pd

from ari_ml.datasets.features import FEATURE_COLUMNS, FEATURE_SCHEMA_VERSION
from ari_ml.inference.schemas import PredictionRequest, PredictionResponse


class ModelCompatibilityError(RuntimeError):
    pass


class ModelPredictor:
    def __init__(self, artifact_dir: Path):
        self.artifact_dir = artifact_dir.resolve()
        metadata_path = self.artifact_dir / "metadata.json"
        artifact_path = self.artifact_dir / "model.joblib"
        if not metadata_path.is_file() or not artifact_path.is_file():
            raise FileNotFoundError("model.joblib and metadata.json are required")

        self.metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        actual_checksum = hashlib.sha256(artifact_path.read_bytes()).hexdigest()
        if actual_checksum != self.metadata["modelArtifactSha256"]:
            raise ModelCompatibilityError("model artifact checksum does not match metadata")
        if self.metadata["featureSchemaVersion"] != FEATURE_SCHEMA_VERSION:
            raise ModelCompatibilityError("feature schema version is incompatible")
        if tuple(self.metadata["featureColumns"]) != FEATURE_COLUMNS:
            raise ModelCompatibilityError("feature columns or order are incompatible")
        expected_sklearn = self.metadata["dependencyVersions"]["scikitLearn"]
        if version("scikit-learn") != expected_sklearn:
            raise ModelCompatibilityError(
                f"scikit-learn {expected_sklearn} is required by the artifact"
            )

        # joblib is pickle-based. Only this checksum-verified, locally trained artifact is trusted.
        bundle = joblib.load(artifact_path)
        if tuple(bundle["feature_columns"]) != FEATURE_COLUMNS:
            raise ModelCompatibilityError("model bundle feature columns are incompatible")
        self.pipeline = bundle["pipeline"]
        self.supported_repair_codes = frozenset(bundle["supported_repair_codes"])

    @property
    def model_version(self) -> str:
        return str(self.metadata["modelVersion"])

    def predict(self, request: PredictionRequest) -> PredictionResponse:
        if request.repair_code not in self.supported_repair_codes:
            raise ValueError(f"unsupported repair code: {request.repair_code}")
        condition_codes = {
            value.strip().upper()
            for value in request.known_conditions + request.extracted_conditions
        }
        row = pd.DataFrame(
            [
                {
                    "vehicle_age_years": request.vehicle_age_years,
                    "mileage_km": request.mileage_km,
                    "baseline_labor_minutes": request.baseline_labor_hours * 60,
                    "known_corrosion": int("VISIBLE_CORROSION" in condition_codes),
                    "repair_code": request.repair_code,
                    "manufacturer": request.manufacturer,
                    "model": request.model,
                }
            ],
            columns=list(FEATURE_COLUMNS),
        )
        minutes = float(self.pipeline.predict(row)[0])
        if not math.isfinite(minutes) or minutes <= 0:
            raise ValueError("model produced a non-positive or non-finite prediction")
        return PredictionResponse(
            predictedLaborHours=minutes / 60,
            modelVersion=self.model_version,
            dataSource=self.metadata["dataClassification"],
            rangeLowerHours=None,
            rangeUpperHours=None,
            predictionMethod="ML_REGRESSION",
            status="PREDICTED",
        )
