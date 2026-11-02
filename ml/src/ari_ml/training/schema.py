"""Leakage-safe feature/label boundary for the first labor-time regression model."""

from ari_ml.datasets.features import FEATURE_COLUMNS, FORBIDDEN_FEATURES

PREDICTION_FEATURES = FEATURE_COLUMNS

LABEL_FIELDS = ("actual_labor_minutes",)

# These columns describe outcomes and are forbidden as prediction inputs.
POST_START_FIELDS = FORBIDDEN_FEATURES + (
    "additional_labor_minutes",
    "performed_operation_codes",
)
def assert_leakage_safe(feature_names: tuple[str, ...]) -> None:
    leaked = sorted(set(feature_names).intersection(POST_START_FIELDS))
    if leaked:
        raise ValueError(f"post-start fields cannot be prediction features: {', '.join(leaked)}")
