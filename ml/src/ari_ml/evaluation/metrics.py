"""Workshop-relevant regression metrics, all expressed in labor minutes."""

from __future__ import annotations

import math

import numpy as np


def regression_metrics(actual, predicted) -> dict[str, float]:
    actual_values = np.asarray(actual, dtype=float)
    predicted_values = np.asarray(predicted, dtype=float)
    if actual_values.shape != predicted_values.shape or actual_values.size == 0:
        raise ValueError("actual and predicted values must have the same non-empty shape")
    if not np.isfinite(actual_values).all() or not np.isfinite(predicted_values).all():
        raise ValueError("metrics require finite values")

    errors = predicted_values - actual_values
    absolute = np.abs(errors)
    return {
        "mae_minutes": float(np.mean(absolute)),
        "rmse_minutes": float(math.sqrt(np.mean(errors**2))),
        "median_absolute_error_minutes": float(np.median(absolute)),
        "p90_absolute_error_minutes": float(np.percentile(absolute, 90)),
        "mean_error_bias_minutes": float(np.mean(errors)),
        "underestimation_rate_percent": float(np.mean(errors < 0) * 100),
    }
