"""Generate deterministic synthetic suspension-repair histories.

The statistical model is intentionally explicit and educational. Nothing in this module should be
interpreted as empirical workshop evidence.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import uuid
from dataclasses import dataclass
from datetime import date, datetime, timedelta, timezone
from pathlib import Path

from ari_ml.settings import SYNTHETIC_DATA_SEED, SYNTHETIC_GENERATOR_VERSION
from ari_ml.synthetic.repair_rules import (
    COMPLICATION_LABOR,
    COMPLICATION_PARTS,
    REPAIR_OPERATIONS,
)
from ari_ml.synthetic.report_templates import render_report
from ari_ml.synthetic.vehicle_profiles import VEHICLE_PROFILES

NAMESPACE = uuid.UUID("a1e2a0dd-c7d5-4b58-b84d-f70bd004c821")


@dataclass(frozen=True)
class GeneratorConfig:
    count: int = 1000
    seed: int = SYNTHETIC_DATA_SEED
    repair_family: str = "SUSPENSION"
    start_date: date = date(2021, 1, 1)
    end_date: date = date(2025, 12, 31)
    generator_version: str = SYNTHETIC_GENERATOR_VERSION
    dataset_id: str = "ari-suspension-synthetic-v1"

    def validate(self) -> None:
        if not 1 <= self.count <= 1000:
            raise ValueError("count must be between 1 and 1000")
        if self.repair_family != "SUSPENSION":
            raise ValueError("ARI-DATA-01 supports only the SUSPENSION repair family")
        if self.start_date > self.end_date:
            raise ValueError("start_date must not be after end_date")
        if not self.dataset_id.strip():
            raise ValueError("dataset_id is required")


def generate_dataset(config: GeneratorConfig) -> dict:
    """Return one JSON-serializable, deterministic dataset."""

    config.validate()
    rng = random.Random(config.seed)
    records = [_generate_case(config, rng, index) for index in range(config.count)]
    canonical_records = json.dumps(
        records, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    checksum = hashlib.sha256(canonical_records).hexdigest()
    return {
        "manifest": {
            "datasetId": config.dataset_id,
            "generatorVersion": config.generator_version,
            "seed": config.seed,
            "recordCount": config.count,
            "dataClassification": "SYNTHETIC",
            "schemaVersion": 2,
            "checksum": checksum,
            "checksumAlgorithm": "SHA256-CANONICAL-JSON",
            "repairFamily": config.repair_family,
            "startDate": config.start_date.isoformat(),
            "endDate": config.end_date.isoformat(),
        },
        "records": records,
    }


def write_dataset(config: GeneratorConfig, output: Path) -> dict:
    dataset = generate_dataset(config)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(dataset, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    return dataset


def _generate_case(config: GeneratorConfig, rng: random.Random, index: int) -> dict:
    repair_day = config.start_date + timedelta(
        days=rng.randint(0, (config.end_date - config.start_date).days)
    )
    profile = rng.choice(VEHICLE_PROFILES)
    manufacturer, model, production_start, production_end = profile
    latest_model_year = min(production_end, repair_day.year - 2)
    earliest_model_year = max(production_start, repair_day.year - 20)
    model_year = rng.randint(earliest_model_year, max(earliest_model_year, latest_model_year))
    age = max(2, repair_day.year - model_year)

    annual_distance = rng.randint(9_000, 25_000)
    mileage = max(15_000, int(age * annual_distance + rng.gauss(0, 8_000)))
    mileage = min(mileage, 420_000)

    climate_exposure = rng.random()
    corrosion_score = _sigmoid(-3.2 + age * 0.19 + mileage / 170_000 + climate_exposure)
    known_corrosion = rng.random() < corrosion_score
    if known_corrosion:
        severity_roll = corrosion_score + rng.uniform(-0.2, 0.2)
        corrosion_severity = (
            "HIGH" if severity_roll > 0.82 else "MODERATE" if severity_roll > 0.55 else "LOW"
        )
    else:
        corrosion_severity = None

    operation_code, assembly_code, baseline_minutes = rng.choice(REPAIR_OPERATIONS)
    order_id = _stable_id(config.dataset_id, index, "order")
    vehicle_id = _stable_id(config.dataset_id, index, "vehicle")
    operation_id = _stable_id(config.dataset_id, index, "operation")
    execution_id = _stable_id(config.dataset_id, index, "execution")

    start_hour = rng.randint(7, 14)
    started_at = datetime(
        repair_day.year, repair_day.month, repair_day.day, start_hour, rng.choice((0, 15, 30, 45)),
        tzinfo=timezone.utc,
    )
    created_at = started_at - timedelta(days=rng.randint(1, 14), hours=rng.randint(0, 8))
    captured_at = created_at - timedelta(minutes=rng.randint(0, 90))

    conditions: list[dict] = []
    condition_codes: list[str] = []
    if known_corrosion:
        condition_codes.append("VISIBLE_CORROSION")
        conditions.append(
            {
                "conditionCode": "VISIBLE_CORROSION",
                "severity": corrosion_severity,
                "documentedAtUtc": _iso(created_at - timedelta(minutes=rng.randint(0, 60))),
            }
        )
    if rng.random() < 0.07 + mileage / 2_500_000:
        condition_codes.append("VISIBLE_PRIOR_DAMAGE")
        conditions.append(
            {
                "conditionCode": "VISIBLE_PRIOR_DAMAGE",
                "severity": rng.choice(("LOW", "MODERATE")),
                "documentedAtUtc": _iso(created_at - timedelta(minutes=rng.randint(0, 45))),
            }
        )

    complication_codes: list[str] = []
    seized_probability = 0.05 + corrosion_score * 0.43 + (0.25 if known_corrosion else 0)
    if rng.random() < min(0.78, seized_probability):
        complication_codes.append("SEIZED_FASTENER")
    broken_probability = 0.035 + (0.20 if "SEIZED_FASTENER" in complication_codes else 0)
    broken_probability += 0.10 if corrosion_severity == "HIGH" else 0
    if rng.random() < broken_probability:
        complication_codes.append("BROKEN_BOLT")
    if rng.random() < min(0.35, 0.04 + mileage / 900_000):
        complication_codes.append("DAMAGED_BUSHING_SEAT")
    if rng.random() < 0.10:
        complication_codes.append("ALIGNMENT_CORRECTION")

    complication_rows: list[dict] = []
    additional_total = 0
    for complication_index, code in enumerate(complication_codes):
        lower, upper = COMPLICATION_LABOR[code]
        extra = rng.randint(lower, upper)
        additional_total += extra
        complication_rows.append(
            {
                "id": _stable_id(config.dataset_id, index, f"complication-{complication_index}"),
                "repairExecutionId": execution_id,
                "complicationCode": code,
                "additionalLaborMinutes": extra,
                "additionalPartCodes": COMPLICATION_PARTS[code],
            }
        )

    efficiency_factor = min(1.18, max(0.82, rng.gauss(0.99, 0.075)))
    unexplained_minutes = round(rng.gauss(0, 8))
    actual_minutes = max(
        35, round(baseline_minutes * efficiency_factor) + additional_total + unexplained_minutes
    )
    elapsed_minutes = actual_minutes + rng.randint(15, 150)
    completed_at = started_at + timedelta(minutes=elapsed_minutes)

    for complication_index, complication in enumerate(complication_rows):
        fraction = (complication_index + 1) / (len(complication_rows) + 1)
        occurred_at = started_at + timedelta(minutes=max(1, int(elapsed_minutes * fraction)))
        complication["occurredAtUtc"] = _iso(occurred_at)

    report = render_report(rng, complication_codes, vague=rng.random() < 0.18)
    description = rng.choice(
        (
            "Customer reports suspension noise over uneven roads.",
            "Control-arm play identified during inspection.",
            "Suspension repair requested after scheduled inspection.",
            None,
        )
    )

    return {
        "vehicle": {
            "id": vehicle_id,
            "manufacturer": manufacturer,
            "model": model,
            "modelYear": model_year,
            "mileageKm": mileage,
            "capturedAtUtc": _iso(captured_at),
        },
        "order": {
            "id": order_id,
            "repairCode": operation_code,
            "problemDescription": description,
            "createdAtUtc": _iso(created_at),
            "knownConditions": condition_codes,
        },
        "operations": [
            {
                "id": operation_id,
                "repairOrderId": order_id,
                "assemblyCode": assembly_code,
                "operationCode": operation_code,
                "referenceLaborHours": round(baseline_minutes / 60, 2),
            }
        ],
        "knownConditions": conditions,
        "execution": {
            "id": execution_id,
            "repairOrderId": order_id,
            "actualLaborMinutes": actual_minutes,
            "startedAtUtc": _iso(started_at),
            "completedAtUtc": _iso(completed_at),
            "performedOperationCodes": [operation_code],
            "repairReport": report,
        },
        "complications": complication_rows,
    }


def _sigmoid(value: float) -> float:
    return 1 / (1 + math.exp(-value))


def _stable_id(dataset_id: str, index: int, entity: str) -> str:
    return str(uuid.uuid5(NAMESPACE, f"{dataset_id}:{index}:{entity}"))


def _iso(value: datetime) -> str:
    return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--count", type=int, default=1000)
    parser.add_argument("--seed", type=int, default=SYNTHETIC_DATA_SEED)
    parser.add_argument("--repair-family", default="SUSPENSION")
    parser.add_argument("--start-date", type=date.fromisoformat, default=date(2021, 1, 1))
    parser.add_argument("--end-date", type=date.fromisoformat, default=date(2025, 12, 31))
    parser.add_argument("--generator-version", default=SYNTHETIC_GENERATOR_VERSION)
    parser.add_argument("--dataset-id", default="ari-suspension-synthetic-v1")
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def main() -> None:
    args = _parse_args()
    config = GeneratorConfig(
        count=args.count,
        seed=args.seed,
        repair_family=args.repair_family,
        start_date=args.start_date,
        end_date=args.end_date,
        generator_version=args.generator_version,
        dataset_id=args.dataset_id,
    )
    dataset = write_dataset(config, args.output)
    print(
        f"Generated {dataset['manifest']['recordCount']} records; "
        f"checksum={dataset['manifest']['checksum']}"
    )


if __name__ == "__main__":
    main()
