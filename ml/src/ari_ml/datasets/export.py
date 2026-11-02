"""Read and verify the deterministic .NET export from SAP HANA."""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True, slots=True)
class ExportManifest:
    dataset_id: str
    generator_version: str
    source_dataset_checksum: str
    record_count: int
    data_classification: str
    export_schema_version: int
    feature_schema_version: str
    export_checksum: str


@dataclass(frozen=True, slots=True)
class TrainingExport:
    manifest: ExportManifest
    records: list[dict]


def load_training_export(path: Path) -> TrainingExport:
    document = json.loads(path.read_text(encoding="utf-8"))
    manifest_data = document["manifest"]
    records = document["records"]
    actual_checksum = canonical_checksum(records)
    expected_checksum = manifest_data["exportChecksum"]
    if actual_checksum != expected_checksum:
        raise ValueError("training export checksum does not match its records")
    if len(records) != manifest_data["recordCount"]:
        raise ValueError("training export record count does not match its manifest")
    if manifest_data["dataClassification"] != "SYNTHETIC":
        raise ValueError("ARI-ML-01 accepts only explicitly synthetic training data")
    if manifest_data["exportSchemaVersion"] != 1:
        raise ValueError("unsupported training export schema version")

    return TrainingExport(
        ExportManifest(
            dataset_id=manifest_data["datasetId"],
            generator_version=manifest_data["generatorVersion"],
            source_dataset_checksum=manifest_data["sourceDatasetChecksum"],
            record_count=manifest_data["recordCount"],
            data_classification=manifest_data["dataClassification"],
            export_schema_version=manifest_data["exportSchemaVersion"],
            feature_schema_version=manifest_data["featureSchemaVersion"],
            export_checksum=manifest_data["exportChecksum"],
        ),
        records,
    )


def canonical_checksum(records: list[dict]) -> str:
    payload = json.dumps(
        records, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()
