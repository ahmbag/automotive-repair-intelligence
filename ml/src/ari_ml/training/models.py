"""scikit-learn pipelines that keep preprocessing and regression inseparable."""

from __future__ import annotations

from sklearn.compose import ColumnTransformer
from sklearn.ensemble import RandomForestRegressor
from sklearn.impute import SimpleImputer
from sklearn.linear_model import Ridge
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder, StandardScaler

from ari_ml.datasets.features import CATEGORICAL_FEATURES, NUMERIC_FEATURES


def build_models(config: dict) -> dict[str, Pipeline]:
    random_state = int(config["experiment"]["random_state"])
    return {
        "ridge": Pipeline(
            [
                ("preprocessor", _preprocessor(scale_numeric=True)),
                ("regressor", Ridge(alpha=float(config["ridge"]["alpha"]))),
            ]
        ),
        "random_forest": Pipeline(
            [
                ("preprocessor", _preprocessor(scale_numeric=False)),
                (
                    "regressor",
                    RandomForestRegressor(
                        n_estimators=int(config["random_forest"]["n_estimators"]),
                        max_depth=int(config["random_forest"]["max_depth"]),
                        min_samples_leaf=int(
                            config["random_forest"]["min_samples_leaf"]
                        ),
                        random_state=random_state,
                        n_jobs=-1,
                    ),
                ),
            ]
        ),
    }


def _preprocessor(scale_numeric: bool) -> ColumnTransformer:
    numeric_steps: list[tuple] = [("imputer", SimpleImputer(strategy="median"))]
    if scale_numeric:
        numeric_steps.append(("scaler", StandardScaler()))
    numeric = Pipeline(numeric_steps)
    categorical = Pipeline(
        [
            ("imputer", SimpleImputer(strategy="most_frequent")),
            ("one_hot", OneHotEncoder(handle_unknown="ignore", sparse_output=False)),
        ]
    )
    return ColumnTransformer(
        [
            ("numeric", numeric, list(NUMERIC_FEATURES)),
            ("categorical", categorical, list(CATEGORICAL_FEATURES)),
        ],
        remainder="drop",
    )
