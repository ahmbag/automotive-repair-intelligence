"""Train, compare, evaluate, and version the ARI labor-time models."""

from __future__ import annotations

import argparse
import hashlib
import json
import platform
import tomllib
from datetime import datetime, timezone
from importlib.metadata import version
from pathlib import Path

import joblib
import numpy as np
from sklearn.inspection import permutation_importance

from ari_ml.baseline.estimators import HistoricalBaselines
from ari_ml.datasets.export import load_training_export
from ari_ml.datasets.features import (
    FEATURE_COLUMNS,
    FEATURE_SCHEMA_VERSION,
    TARGET_COLUMN,
    build_training_frame,
)
from ari_ml.datasets.split import chronological_split
from ari_ml.evaluation.metrics import regression_metrics
from ari_ml.training.models import build_models


def train(config_path: Path, export_path: Path, output_dir: Path) -> dict:
    config = tomllib.loads(config_path.read_text(encoding="utf-8"))
    export = load_training_export(export_path)
    _validate_config(config, export.manifest)
    frame = build_training_frame(export.records)
    split = chronological_split(
        frame,
        float(config["split"]["train_fraction"]),
        float(config["split"]["validation_fraction"]),
    )

    validation_results = _evaluate_validation(config, split.train, split.validation)
    selected_model_name = min(
        ("ridge", "random_forest"),
        key=lambda name: validation_results[name]["mae_minutes"],
    )

    development = _concat(split.train, split.validation)
    baselines = HistoricalBaselines(int(config["baseline"]["minimum_group_size"])).fit(
        development
    )
    test_results = {
        name: regression_metrics(split.test[TARGET_COLUMN], predictions)
        for name, predictions in baselines.predict_all(split.test).items()
    }

    final_models = build_models(config)
    final_predictions: dict[str, np.ndarray] = {}
    for name, model in final_models.items():
        model.fit(development[list(FEATURE_COLUMNS)], development[TARGET_COLUMN])
        predictions = model.predict(split.test[list(FEATURE_COLUMNS)])
        if not np.isfinite(predictions).all() or (predictions <= 0).any():
            raise ValueError(f"{name} produced an invalid final prediction")
        final_predictions[name] = predictions
        test_results[name] = regression_metrics(split.test[TARGET_COLUMN], predictions)

    selected_model = final_models[selected_model_name]
    selected_predictions = final_predictions[selected_model_name]
    importance = permutation_importance(
        selected_model,
        split.test[list(FEATURE_COLUMNS)],
        split.test[TARGET_COLUMN],
        scoring="neg_mean_absolute_error",
        n_repeats=int(config["evaluation"]["permutation_repeats"]),
        random_state=int(config["experiment"]["random_state"]),
        n_jobs=-1,
    )
    feature_importance = sorted(
        (
            {
                "feature": feature,
                "mean_mae_increase_minutes": float(mean),
                "standard_deviation": float(std),
            }
            for feature, mean, std in zip(
                FEATURE_COLUMNS, importance.importances_mean, importance.importances_std, strict=True
            )
        ),
        key=lambda row: row["mean_mae_increase_minutes"],
        reverse=True,
    )
    segments = _segment_analysis(split.test, selected_predictions)
    best_baseline_name = min(
        ("reference_labor", "historical_median", "median_uplift"),
        key=lambda name: validation_results[name]["mae_minutes"],
    )
    recommended_strategy = _recommend_strategy(
        validation_results, selected_model_name, best_baseline_name
    )

    output_dir.mkdir(parents=True, exist_ok=True)
    artifact_path = output_dir / "model.joblib"
    model_bundle = {
        "pipeline": selected_model,
        "feature_columns": FEATURE_COLUMNS,
        "feature_schema_version": FEATURE_SCHEMA_VERSION,
        "supported_repair_codes": sorted(frame["repair_code"].unique().tolist()),
    }
    joblib.dump(model_bundle, artifact_path, compress=3)
    artifact_checksum = hashlib.sha256(artifact_path.read_bytes()).hexdigest()

    report = {
        "experimentName": config["experiment"]["name"],
        "modelVersion": config["experiment"]["model_version"],
        "selectedMlModel": selected_model_name,
        "recommendedStrategy": recommended_strategy,
        "bestBaseline": best_baseline_name,
        "strategySelectionSource": "validation_mae",
        "split": {
            "strategy": "chronological_by_completed_at_utc",
            "trainCount": len(split.train),
            "validationCount": len(split.validation),
            "testCount": len(split.test),
            "trainEndUtc": split.train_end_utc,
            "validationEndUtc": split.validation_end_utc,
        },
        "validationMetrics": validation_results,
        "testMetrics": test_results,
        "segmentedTestAnalysis": segments,
        "permutationImportance": feature_importance,
    }
    metadata = {
        "modelVersion": config["experiment"]["model_version"],
        "selectedModel": selected_model_name,
        "predictionMethod": "ML_REGRESSION",
        "recommendedStrategy": recommended_strategy,
        "dataClassification": export.manifest.data_classification,
        "datasetId": export.manifest.dataset_id,
        "datasetSha256": export.manifest.source_dataset_checksum,
        "trainingExportSha256": export.manifest.export_checksum,
        "generatorVersion": export.manifest.generator_version,
        "featureSchemaVersion": FEATURE_SCHEMA_VERSION,
        "featureColumns": list(FEATURE_COLUMNS),
        "trainingConfiguration": config,
        "dependencyVersions": {
            "python": platform.python_version(),
            "numpy": version("numpy"),
            "pandas": version("pandas"),
            "scikitLearn": version("scikit-learn"),
            "joblib": version("joblib"),
        },
        "evaluationMetrics": test_results[selected_model_name],
        "modelArtifactSha256": artifact_checksum,
        "trainedAtUtc": datetime.now(timezone.utc).isoformat(),
        "trustedArtifactNotice": "Load only artifacts produced by this repository's training command.",
    }
    _write_json(output_dir / "evaluation.json", report)
    _write_json(output_dir / "metadata.json", metadata)
    return {"report": report, "metadata": metadata}


def _evaluate_validation(config: dict, train_frame, validation_frame) -> dict:
    baselines = HistoricalBaselines(int(config["baseline"]["minimum_group_size"])).fit(
        train_frame
    )
    results = {
        name: regression_metrics(validation_frame[TARGET_COLUMN], prediction)
        for name, prediction in baselines.predict_all(validation_frame).items()
    }
    for name, model in build_models(config).items():
        model.fit(train_frame[list(FEATURE_COLUMNS)], train_frame[TARGET_COLUMN])
        prediction = model.predict(validation_frame[list(FEATURE_COLUMNS)])
        results[name] = regression_metrics(validation_frame[TARGET_COLUMN], prediction)
    return results


def _segment_analysis(frame, predictions) -> dict:
    evaluated = frame.assign(_prediction=predictions)
    deviation = evaluated[TARGET_COLUMN] - evaluated["baseline_labor_minutes"]
    masks = {
        "older_vehicles_age_12_plus": evaluated["vehicle_age_years"] >= 12,
        "high_mileage_200k_plus": evaluated["mileage_km"] >= 200_000,
        "known_corrosion": evaluated["known_corrosion"] == 1,
        "high_actual_uplift_60_plus": deviation >= 60,
    }
    result = {}
    for name, mask in masks.items():
        segment = evaluated[mask]
        result[name] = {
            "count": len(segment),
            "metrics": regression_metrics(segment[TARGET_COLUMN], segment["_prediction"])
            if len(segment)
            else None,
        }
    return result


def _concat(first, second):
    import pandas as pd

    return pd.concat([first, second], ignore_index=True)


def _recommend_strategy(
    validation_results: dict, selected_model_name: str, best_baseline_name: str
) -> str:
    """Choose deployment strategy without looking at the independent test labels."""

    return (
        "ML_REGRESSION"
        if validation_results[selected_model_name]["mae_minutes"]
        < validation_results[best_baseline_name]["mae_minutes"]
        else "STATISTICAL_BASELINE"
    )


def _validate_config(config: dict, manifest) -> None:
    if config["dataset"]["dataset_id"] != manifest.dataset_id:
        raise ValueError("training configuration dataset ID does not match the export")
    if config["dataset"]["data_classification"] != manifest.data_classification:
        raise ValueError("training configuration data classification does not match")
    if config["experiment"]["feature_schema_version"] != FEATURE_SCHEMA_VERSION:
        raise ValueError("training configuration feature schema is incompatible")
    if manifest.feature_schema_version != FEATURE_SCHEMA_VERSION:
        raise ValueError("training export feature schema is incompatible")


def _write_json(path: Path, value: dict) -> None:
    path.write_text(
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    return parser.parse_args()


def main() -> None:
    args = _parse_args()
    result = train(args.config, args.dataset, args.output_dir)
    report = result["report"]
    print(f"Selected ML model: {report['selectedMlModel']}")
    print(f"Recommended strategy: {report['recommendedStrategy']}")
    for name, metrics in report["testMetrics"].items():
        print(
            f"{name}: MAE={metrics['mae_minutes']:.3f} min, "
            f"RMSE={metrics['rmse_minutes']:.3f} min, "
            f"bias={metrics['mean_error_bias_minutes']:.3f} min"
        )


if __name__ == "__main__":
    main()
