import json
import unittest
from datetime import date, datetime

from ari_ml.synthetic.generator import GeneratorConfig, generate_dataset


class SyntheticGeneratorTests(unittest.TestCase):
    def test_same_configuration_is_byte_for_byte_deterministic(self) -> None:
        config = GeneratorConfig(count=25, seed=42, dataset_id="determinism-test")

        first = generate_dataset(config)
        second = generate_dataset(config)

        self.assertEqual(first, second)
        self.assertEqual(
            json.dumps(first, ensure_ascii=False, sort_keys=True),
            json.dumps(second, ensure_ascii=False, sort_keys=True),
        )

    def test_different_seed_changes_checksum(self) -> None:
        first = generate_dataset(GeneratorConfig(count=20, seed=1, dataset_id="seed-test"))
        second = generate_dataset(GeneratorConfig(count=20, seed=2, dataset_id="seed-test"))
        self.assertNotEqual(first["manifest"]["checksum"], second["manifest"]["checksum"])

    def test_cases_obey_temporal_and_labor_rules(self) -> None:
        dataset = generate_dataset(GeneratorConfig(count=100, seed=20261023))

        for record in dataset["records"]:
            captured = _parse(record["vehicle"]["capturedAtUtc"])
            created = _parse(record["order"]["createdAtUtc"])
            started = _parse(record["execution"]["startedAtUtc"])
            completed = _parse(record["execution"]["completedAtUtc"])
            self.assertLessEqual(captured, created)
            self.assertLessEqual(created, started)
            self.assertLessEqual(started, completed)
            self.assertGreater(record["operations"][0]["referenceLaborHours"], 0)
            self.assertGreater(record["execution"]["actualLaborMinutes"], 0)
            for event in record["complications"]:
                self.assertGreaterEqual(_parse(event["occurredAtUtc"]), started)
                self.assertLessEqual(_parse(event["occurredAtUtc"]), completed)

    def test_known_corrosion_has_higher_average_time_deviation(self) -> None:
        records = generate_dataset(GeneratorConfig(count=1000, seed=20261023))["records"]

        known = [_deviation(row) for row in records if "VISIBLE_CORROSION" in row["order"]["knownConditions"]]
        unknown = [_deviation(row) for row in records if "VISIBLE_CORROSION" not in row["order"]["knownConditions"]]

        self.assertGreater(len(known), 100)
        self.assertGreater(len(unknown), 100)
        self.assertGreater(sum(known) / len(known), sum(unknown) / len(unknown) + 15)

    def test_reports_and_structured_events_are_both_present(self) -> None:
        records = generate_dataset(GeneratorConfig(count=100, seed=99))["records"]
        self.assertTrue(all(row["execution"]["repairReport"] for row in records))
        self.assertTrue(any(row["complications"] for row in records))
        self.assertTrue(any(not row["complications"] for row in records))

    def test_default_acceptance_range_and_dates_are_declared(self) -> None:
        config = GeneratorConfig()
        self.assertEqual(1000, config.count)
        self.assertEqual(date(2021, 1, 1), config.start_date)
        self.assertEqual(date(2025, 12, 31), config.end_date)


def _parse(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def _deviation(record: dict) -> float:
    baseline = record["operations"][0]["referenceLaborHours"] * 60
    return record["execution"]["actualLaborMinutes"] - baseline


if __name__ == "__main__":
    unittest.main()
