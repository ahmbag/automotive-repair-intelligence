# ADR 0002: Direct, TLS-validated SAP HANA connectivity

- Status: Accepted
- Date: 2026-10-22

## Context

The .NET application needs its first authenticated connection to SAP HANA Cloud while preserving
the existing rule that Core remains vendor-neutral. Liveness must not fail merely because an
external dependency is unavailable, while deployment readiness must represent real database
authentication and query execution.

## Decision

Use SAP's official `Sap.Data.Hana.Net.v10.0` provider in `ARI.Infrastructure`. Bind discrete HANA
endpoint and credential values through the ASP.NET Core Options pattern. `HanaConnectionFactory`
constructs every connection centrally and unconditionally enables transport encryption and server
certificate validation. Raw connection strings are not accepted as configuration.

`HanaConnectivityProbe` opens the connection and executes
`SELECT CURRENT_USER FROM DUMMY`, checking the result against the configured user. The API exposes
this through `/health/ready`; failures are sanitized and mapped to HTTP 503. `/health` remains an
in-process liveness check and never resolves or calls the HANA adapter.

## Consequences

SAP types and SQL stay out of Core and API endpoints. Configuration errors do not prevent the API
from starting, so liveness remains useful, but readiness fails until valid secrets and network
access are present. Certificate failures must be fixed through trusted CA configuration; ARI has no
switch that permits insecure connections.
