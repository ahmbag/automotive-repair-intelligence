import unittest

from ari_ml import GENERATOR_VERSION, RANDOM_SEED
from ari_ml.inference import LaborPredictionInput


class ContractTests(unittest.TestCase):
    def test_reproducibility_identifiers_are_stable(self) -> None:
        self.assertEqual(1729, RANDOM_SEED)
        self.assertEqual("synthetic-v1", GENERATOR_VERSION)

    def test_prediction_contract_contains_only_prediction_time_fields(self) -> None:
        fields = LaborPredictionInput.__dataclass_fields__

        self.assertIn("mileage_km", fields)
        self.assertIn("baseline_labor_hours", fields)
        self.assertNotIn("actual_labor_minutes", fields)
        self.assertNotIn("complications", fields)


if __name__ == "__main__":
    unittest.main()
