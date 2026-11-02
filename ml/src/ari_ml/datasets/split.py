"""Frozen chronological train/validation/test split."""

from __future__ import annotations

from dataclasses import dataclass

import pandas as pd


@dataclass(frozen=True, slots=True)
class ChronologicalSplit:
    train: pd.DataFrame
    validation: pd.DataFrame
    test: pd.DataFrame
    train_end_utc: str
    validation_end_utc: str


def chronological_split(
    frame: pd.DataFrame,
    train_fraction: float = 0.70,
    validation_fraction: float = 0.15,
) -> ChronologicalSplit:
    if not 0 < train_fraction < 1:
        raise ValueError("train_fraction must be between zero and one")
    if not 0 < validation_fraction < 1 - train_fraction:
        raise ValueError("validation_fraction leaves no independent test set")

    ordered = frame.sort_values(
        ["completed_at_utc", "repair_order_id"], kind="stable"
    ).reset_index(drop=True)
    train_count = int(len(ordered) * train_fraction)
    validation_count = int(len(ordered) * validation_fraction)
    train = ordered.iloc[:train_count].copy()
    validation = ordered.iloc[train_count : train_count + validation_count].copy()
    test = ordered.iloc[train_count + validation_count :].copy()

    if min(len(train), len(validation), len(test)) == 0:
        raise ValueError("every chronological partition must contain records")
    if train["completed_at_utc"].max() > validation["completed_at_utc"].min():
        raise ValueError("training completion times overlap validation")
    if validation["completed_at_utc"].max() > test["completed_at_utc"].min():
        raise ValueError("validation completion times overlap test")

    # The fingerprint is intentionally precise. Exact synthetic twins must not cross partitions.
    fingerprints = {
        name: set(_fingerprints(partition))
        for name, partition in (("train", train), ("validation", validation), ("test", test))
    }
    if fingerprints["train"] & fingerprints["validation"]:
        raise ValueError("exact feature twins cross train and validation")
    if fingerprints["train"] & fingerprints["test"]:
        raise ValueError("exact feature twins cross train and test")
    if fingerprints["validation"] & fingerprints["test"]:
        raise ValueError("exact feature twins cross validation and test")

    return ChronologicalSplit(
        train,
        validation,
        test,
        train["completed_at_utc"].max().isoformat(),
        validation["completed_at_utc"].max().isoformat(),
    )


def _fingerprints(frame: pd.DataFrame) -> list[tuple]:
    columns = [
        "manufacturer",
        "model",
        "vehicle_age_years",
        "mileage_km",
        "repair_code",
        "baseline_labor_minutes",
        "known_corrosion",
    ]
    return list(frame[columns].itertuples(index=False, name=None))
