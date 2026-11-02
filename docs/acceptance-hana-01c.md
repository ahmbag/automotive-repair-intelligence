# ARI-HANA-01C acceptance report

Status: **complete — 13/13 formal Definition-of-Done points passed**  
Verified: 2026-10-23 against the configured SAP HANA Cloud instance

| # | Definition of Done | Status | Evidence |
| ---: | --- | :---: | --- |
| 1 | Versioned changes executed by `ARI_DEPLOY` | Pass | First run reported `Applied: 001_create_repair_order_foundation`; repeat run reported `Already applied`, proving tracking in `ARI.SCHEMA_MIGRATIONS` |
| 2 | Both relational tables exist | Pass | Live CRUD succeeded against `ARI.VEHICLE_SNAPSHOTS` and `ARI.REPAIR_ORDERS` |
| 3 | PK and FK effective | Pass | Live duplicate-ID and missing-vehicle tests were rejected by HANA |
| 4 | SAP-independent Core contracts | Pass | Three ports are in `ARI.Core`; SAP package and SQL remain in `ARI.Infrastructure` |
| 5 | Write/read with `ARI_APP` | Pass | All live integration tests authenticated and performed DML as `ARI_APP` |
| 6 | Both objects load together | Pass | Joined repository test reconstructed `RepairOrder` and `VehicleSnapshot` |
| 7 | GUID/timestamp/optional values mapped | Pass | Live standard GUID, exact UTC, SQL `NULL`, Unicode, JSON conditions, and normalized code assertions passed |
| 8 | Joint save in one transaction | Pass | Aggregate implementation uses one connection and transaction for both inserts |
| 9 | Deliberate failure fully rolls back | Pass | Duplicate order ID after snapshot insert failed; follow-up read proved the new snapshot absent |
| 10 | Invalid references/duplicates controlled | Pass | HANA codes 461/301 translate to integrity/conflict exceptions |
| 11 | Real HANA integration tests pass | Pass | `Category=HanaIntegration`: 9/9 passed, 0 skipped |
| 12 | Solution build/regressions pass | Pass | Build: 0 warnings/errors; existing Core/API suite: 9/9 passed |
| 13 | Architecture/persistence docs updated | Pass | Architecture, persistence guide, ADR 0003, README, migration, and this report |

## Automated evidence

```text
dotnet build AutomotiveRepairIntelligence.slnx --no-restore
Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test AutomotiveRepairIntelligence.slnx --no-build --filter "Category!=HanaIntegration"
ARI.Core.Tests: 3 passed
ARI.Api.Tests: 6 passed

dotnet test tests/ARI.Infrastructure.IntegrationTests/ARI.Infrastructure.IntegrationTests.csproj --no-build --filter "Category=HanaIntegration"
ARI.Infrastructure.IntegrationTests: 9 passed, 0 failed, 0 skipped

python -m unittest discover -s ml/tests -v (with ml/src on PYTHONPATH)
Python ML contract tests: 2 passed
```

The live migration used temporary process-only `ARI_DEPLOY` credentials. The test run used temporary
process-only `ARI_APP` credentials. Neither password nor a connection string was written to source,
configuration files, test output, or command-line arguments. Every integration test cleaned up only
its own generated rows.
