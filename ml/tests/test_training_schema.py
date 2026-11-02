import unittest

from ari_ml.training.schema import (
    PREDICTION_FEATURES,
    assert_leakage_safe,
)


class TrainingSchemaTests(unittest.TestCase):
    def test_declared_prediction_features_are_leakage_safe(self) -> None:
        assert_leakage_safe(PREDICTION_FEATURES)

    def test_post_start_complication_is_rejected_as_feature(self) -> None:
        with self.assertRaisesRegex(ValueError, "complication_codes"):
            assert_leakage_safe(PREDICTION_FEATURES + ("complication_codes",))

    def test_target_is_rejected_as_feature(self) -> None:
        with self.assertRaisesRegex(ValueError, "actual_labor_minutes"):
            assert_leakage_safe(PREDICTION_FEATURES + ("actual_labor_minutes",))

if __name__ == "__main__":
    unittest.main()
