# SAP HANA relational persistence

## Ownership and schema deployment

`ARI_DEPLOY` is the deployment identity. It runs ordered, forward-only SQL files in
`database/hana` through `tools/ARI.DatabaseMigrator`. The runner creates and reads
`ARI.SCHEMA_MIGRATIONS`, skips recorded versions, and records a version only after every statement
in that file succeeds. Migration files must never reset or drop the schema.

Set `Hana__Host`, `Hana__Port`, `Hana__User`, and `Hana__Password` in the process environment, with
`Hana__User=ARI_DEPLOY`, then run:

```powershell
dotnet run --project tools/ARI.DatabaseMigrator -- database/hana
```

Do not place credentials in a migration, source file, command-line argument, or log. Migration 001
creates `ARI.VEHICLE_SNAPSHOTS` and `ARI.REPAIR_ORDERS`, the primary and foreign keys, and grants
table-level DML privileges to `ARI_APP`.

## Runtime model

`ARI_APP` is the runtime identity. All database implementation details stay in
`ARI.Infrastructure`; `ARI.Core` exposes only provider-neutral repository and aggregate-persistence
contracts. SQL values use ordered parameters, never string interpolation.

| C# value | HANA representation |
| --- | --- |
| `Guid` | lowercase/standard `NVARCHAR(36)` (`D` format) |
| `DateTimeOffset` | UTC `TIMESTAMP`; reads explicitly restore the UTC offset |
| nullable description | SQL `NULL` / `DBNull.Value` |
| repair code | trimmed, invariant uppercase `NVARCHAR(100)` |
| known conditions | JSON array in `NVARCHAR(5000)`; malformed JSON fails visibly |

`HanaRepairOrderPersistence.CreateAsync` opens one connection, begins one transaction, inserts the
snapshot followed by the repair order, and commits only after both succeed. Every exception causes
an explicit rollback. HANA native code 301 is exposed as `PersistenceConflictException`; foreign-key
code 461 is exposed as `PersistenceIntegrityException`. Other provider failures are sanitized.

## Verification and cleanup

Real tests require the `ARI_APP` environment configuration and are deliberately marked:

```powershell
dotnet test tests/ARI.Infrastructure.IntegrationTests/ARI.Infrastructure.IntegrationTests.csproj --filter "Category=HanaIntegration"
```

They use generated GUIDs and delete only the rows they created, orders before snapshots. They never
truncate, drop, recreate, or otherwise reset the schema. Without configuration, integration cases
are reported as skipped rather than falsely passing.
