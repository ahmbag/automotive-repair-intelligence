import tempfile
import unittest
from pathlib import Path

from fastapi.testclient import TestClient

from ari_ml.inference.api import create_app


class InferenceApiTests(unittest.TestCase):
    def test_health_is_independent_when_model_is_missing(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = TestClient(create_app(Path(directory)))
            self.assertEqual(200, client.get("/health").status_code)
            self.assertEqual("Healthy", client.get("/health").json()["status"])

    def test_readiness_and_prediction_fail_when_model_is_missing(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = TestClient(create_app(Path(directory)))
            self.assertEqual(503, client.get("/health/ready").status_code)
            response = client.post("/predict", json=_valid_request())
            self.assertEqual(503, response.status_code)

    def test_invalid_request_is_rejected_before_model_access(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = TestClient(create_app(Path(directory)))
            request = _valid_request()
            request["mileageKm"] = -1
            self.assertEqual(422, client.post("/predict", json=request).status_code)


class TrainedArtifactAcceptanceTests(unittest.TestCase):
    """Exercises the real locally trained artifact when the reproducible build produced it."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.artifact_dir = Path("artifacts/ml/ari-repair-duration-v1")
        if not (cls.artifact_dir / "model.joblib").is_file():
            raise unittest.SkipTest("run ari-ml-train to create the acceptance artifact")
        cls.client = TestClient(create_app(cls.artifact_dir))

    def test_real_artifact_is_ready_with_expected_versions(self) -> None:
        response = self.client.get("/health/ready")
        self.assertEqual(200, response.status_code)
        self.assertEqual("ari-repair-duration-v1", response.json()["modelVersion"])
        self.assertEqual("ari-labor-features-v1", response.json()["featureSchemaVersion"])

    def test_real_prediction_is_positive_and_deterministic(self) -> None:
        first = self.client.post("/predict", json=_valid_request())
        second = self.client.post("/predict", json=_valid_request())
        self.assertEqual(200, first.status_code)
        self.assertEqual(first.json(), second.json())
        self.assertGreater(first.json()["predictedLaborHours"], 0)
        self.assertEqual("ML_REGRESSION", first.json()["predictionMethod"])
        self.assertEqual("PREDICTED", first.json()["status"])

    def test_unknown_repair_code_is_rejected_but_service_stays_ready(self) -> None:
        request = _valid_request()
        request["repairCode"] = "NOT_TRAINED"
        self.assertEqual(422, self.client.post("/predict", json=request).status_code)
        self.assertEqual(200, self.client.get("/health/ready").status_code)


def _valid_request() -> dict:
    return {
        "repairCode": "CONTROL_ARM_FRONT_LEFT_REPLACE",
        "manufacturer": "Volkswagen",
        "model": "Golf VII",
        "vehicleAgeYears": 12,
        "mileageKm": 215_000,
        "baselineLaborHours": 1.8,
        "knownConditions": ["VISIBLE_CORROSION"],
        "extractedConditions": [],
    }


if __name__ == "__main__":
    unittest.main()
