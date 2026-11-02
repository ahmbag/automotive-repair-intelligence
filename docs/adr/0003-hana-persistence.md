# ADR 0003: HANA persistence for the repair-order aggregate

- Status: Accepted
- Date: 2026-10-23

## Context

The first relational slice must persist and reload the vehicle facts captured for a repair order.
The two records must never become partially visible, Core must remain independent of SAP, and schema
changes need repeatable ownership separate from the runtime identity.

## Decision

Store snapshots in `ARI.VEHICLE_SNAPSHOTS` and orders in `ARI.REPAIR_ORDERS`, linked by a restrictive
foreign key. GUIDs use `NVARCHAR(36)`, timestamps use UTC `TIMESTAMP`, nullable descriptions use SQL
`NULL`, and the order's condition collection is a JSON array because it is part of the current Core
model but does not warrant a third domain table in this slice.

Core defines separate snapshot/order repository ports and one aggregate-level create operation.
Infrastructure implements all SQL with SAP's provider and ordered parameters. The aggregate create
uses one connection and one transaction for both inserts. A direct joined read reconstructs both
objects in one database operation.

Forward-only SQL migrations are applied by `ARI_DEPLOY` and tracked in
`ARI.SCHEMA_MIGRATIONS`; `ARI_APP` has only required DML grants.

## Consequences

Referential integrity and duplicate identifiers are enforced by HANA and translated into controlled,
provider-neutral Core exceptions. A failed order insert rolls back the preceding snapshot insert.
Changing the condition representation later requires another migration and an explicit compatibility
decision. Test cleanup targets generated IDs only; destructive schema resets are prohibited.
