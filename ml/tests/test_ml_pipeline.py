import json
import tempfile
import unittest
from pathlib import Path

import numpy as np
import pandas as pd

from ari_ml.baseline.estimators import HistoricalBaselines
from ari_ml.datasets.export import canonical_checksum, load_training_export
from ari_ml.datasets.features import (
    FEATURE_COLUMNS,
    assert_feature_schema_safe,
    build_training_frame,
)
from ari_ml.datasets.split import chronological_split
from ari_ml.evaluation.metrics import regression_metrics
from ari_ml.training.train import _recommend_strategy


class MlPipelineTests(unittest.TestCase):
    def test_strategy_recommendation_uses_validation_metrics(self) -> None:
        validation = {
            "ridge": {"mae_minutes": 20.1},
            "historical_median": {"mae_minutes": 20.0},
        }
        self.assertEqual(
            "STATISTICAL_BASELINE",
            _recommend_strategy(validation, "ridge", "historical_median"),
        )

    def test_export_checksum_tampering_is_rejected(self) -> None:
        payload = {
            "manifest": {
                "datasetId": "test",
                "generatorVersion": "1",
                "sourceDatasetChecksum": "source",
                "recordCount": 1,
                "dataClassification": "SYNTHETIC",
                "exportSchemaVersion": 1,
                "featureSchemaVersion": "ari-labor-features-v1",
                "exportChecksum": "wrong",
            },
            "records": [{"value": 1}],
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "export.json"
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "checksum"):
                load_training_export(path)

    def test_canonical_export_checksum_ignores_property_order(self) -> None:
        self.assertEqual(canonical_checksum([{"b": 2, "a": 1}]), canonical_checksum([{"a": 1, "b": 2}]))

    def test_feature_schema_rejects_outcome(self) -> None:
        with self.assertRaisesRegex(ValueError, "leaked"):
            assert_feature_schema_safe(FEATURE_COLUMNS + ("actual_labor_minutes",))

    def test_chronological_split_is_70_15_15_and_non_overlapping(self) -> None:
        frame = build_training_frame([_record(index) for index in range(100)])
        split = chronological_split(frame)
        self.assertEqual(70, len(split.train))
        self.assertEqual(15, len(split.validation))
        self.assertEqual(15, len(split.test))
        self.assertLessEqual(
            split.train["completed_at_utc"].max(),
            split.validation["completed_at_utc"].min(),
        )
        self.assertLessEqual(
            split.validation["completed_at_utc"].max(),
            split.test["completed_at_utc"].min(),
        )

    def test_historical_baselines_learn_only_from_supplied_history(self) -> None:
        history = pd.DataFrame(
            {
                "repair_code": ["A"] * 3,
                "actual_labor_minutes": [100, 120, 140],
                "baseline_labor_minutes": [90, 90, 90],
            }
        )
        future = pd.DataFrame(
            {
                "repair_code": ["A"],
                "actual_labor_minutes": [10_000],
                "baseline_labor_minutes": [90],
            }
        )
        baseline = HistoricalBaselines(minimum_group_size=2).fit(history)
        self.assertEqual(120, baseline.predict_historical_median(future)[0])
        self.assertEqual(120, baseline.predict_median_uplift(future)[0])

    def test_regression_metrics_use_prediction_minus_actual_for_bias(self) -> None:
        metrics = regression_metrics([100, 200], [90, 230])
        self.assertEqual(20, metrics["mae_minutes"])
        self.assertEqual(10, metrics["mean_error_bias_minutes"])
        self.assertEqual(50, metrics["underestimation_rate_percent"])
        self.assertAlmostEqual(np.sqrt(500), metrics["rmse_minutes"])


def _record(index: int) -> dict:
    completed = pd.Timestamp("2021-01-01T12:00:00Z") + pd.Timedelta(days=index)
    created = completed - pd.Timedelta(days=1)
    return {
        "repairOrderId": f"00000000-0000-0000-0000-{index:012d}",
        "repairOperationId": f"10000000-0000-0000-0000-{index:012d}",
        "createdAtUtc": created.isoformat(),
        "completedAtUtc": completed.isoformat(),
        "vehicleAgeYears": 5 + index % 15,
        "mileageKm": 50_000 + index * 1001,
        "manufacturer": "Example",
        "model": f"M{index % 3}",
        "repairCode": "CONTROL_ARM_FRONT_LEFT_REPLACE",
        "baselineLaborMinutes": 108,
        "knownCorrosion": index % 2 == 0,
        "actualLaborMinutes": 110 + index % 40,
        "complicationCodes": [],
        "repairReport": "Completed.",
    }


if __name__ == "__main__":
    unittest.main()
