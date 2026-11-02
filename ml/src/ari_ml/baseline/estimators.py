"""Three baseline strategies evaluated on exactly the same rows as ML models."""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
import pandas as pd


@dataclass(slots=True)
class HistoricalBaselines:
    minimum_group_size: int = 10
    actual_median_by_code: dict[str, float] = field(init=False, default_factory=dict)
    uplift_median_by_code: dict[str, float] = field(init=False, default_factory=dict)
    global_uplift_median: float = field(init=False, default=0.0)

    def fit(self, history: pd.DataFrame) -> "HistoricalBaselines":
        if history.empty:
            raise ValueError("baseline history must not be empty")
        grouped = history.groupby("repair_code", observed=True)
        counts = grouped["actual_labor_minutes"].count()
        eligible = counts[counts >= self.minimum_group_size].index
        self.actual_median_by_code = (
            grouped["actual_labor_minutes"].median().loc[eligible].to_dict()
        )
        residuals = history["actual_labor_minutes"] - history["baseline_labor_minutes"]
        residual_frame = history.assign(_residual=residuals)
        self.uplift_median_by_code = (
            residual_frame.groupby("repair_code", observed=True)["_residual"]
            .median()
            .loc[eligible]
            .to_dict()
        )
        self.global_uplift_median = float(np.median(residuals))
        return self

    @staticmethod
    def predict_reference(rows: pd.DataFrame) -> np.ndarray:
        return rows["baseline_labor_minutes"].to_numpy(dtype=float)

    def predict_historical_median(self, rows: pd.DataFrame) -> np.ndarray:
        # A previously unseen/rare repair code falls back to its provided reference labor time.
        return np.array(
            [
                self.actual_median_by_code.get(code, baseline)
                for code, baseline in zip(
                    rows["repair_code"], rows["baseline_labor_minutes"], strict=True
                )
            ],
            dtype=float,
        )

    def predict_median_uplift(self, rows: pd.DataFrame) -> np.ndarray:
        # For a rare code the broad fallback is the global historical residual, not future rows.
        return np.array(
            [
                baseline
                + self.uplift_median_by_code.get(code, self.global_uplift_median)
                for code, baseline in zip(
                    rows["repair_code"], rows["baseline_labor_minutes"], strict=True
                )
            ],
            dtype=float,
        )

    def predict_all(self, rows: pd.DataFrame) -> dict[str, np.ndarray]:
        return {
            "reference_labor": self.predict_reference(rows),
            "historical_median": self.predict_historical_median(rows),
            "median_uplift": self.predict_median_uplift(rows),
        }
