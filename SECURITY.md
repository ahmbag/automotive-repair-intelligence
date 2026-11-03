# Security policy

## Supported scope

`ARI-RC1` is a local, synthetic-data engineering demonstrator. The source repository is suitable for public review, but the application is **not approved for an unauthenticated public deployment**. It has no user/tenant authorization boundary and its AI endpoints can cause provider cost.

The following deployment controls are mandatory before exposing a live instance to the internet:

- authentication and authorization in front of UI and API;
- per-identity and infrastructure-level rate limits for extraction, embedding, retrieval, explanation, and prediction endpoints;
- managed secret storage, restricted network access, TLS termination, and environment-specific CORS/host policy;
- request-size, concurrency, budget, telemetry-retention, and abuse controls;
- tenant isolation and an approved data-protection process before any non-synthetic data is used.

Fixture mode is the appropriate public demonstration mode until those controls exist.

## Reporting a vulnerability

Do not disclose suspected credentials or vulnerabilities in a public issue. After publication, use GitHub private vulnerability reporting for this repository. Before publication, contact the repository owner through the private channel by which access to the project was provided.

Include the affected revision, reproduction steps, impact, and whether any credential may have been exposed. Never include working secrets in a report.

## Secrets

Runtime credentials are accepted only through .NET User Secrets, environment variables, or a deployment secret store. They must never be placed in `appsettings*.json`, `.env` files committed to Git, command examples, screenshots, logs, test reports, or issue content.

If a key is pasted into chat, a terminal recording, or another non-secret channel, revoke and rotate it. Deleting the visible message is not a substitute for rotation.

Run the repository gate before every release candidate:

```powershell
./tools/security-audit.ps1
```

## Data and privacy

The committed datasets, reports, and screenshots are synthetic. ARI-RC1 must not process real customer, vehicle-owner, employee, insurer, or workshop records. Logs are designed around correlation IDs, status, version, and latency; prompts, provider bodies, authorization headers, and credentials must not be logged.

The UI currently imports fonts from Google Fonts at runtime. No font binary is redistributed by this repository, but a browser may contact that external service. A privacy-sensitive deployment should self-host approved font files or remove the import.

## Dependency boundary

The repository's own code is MIT licensed. Third-party packages retain their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). In particular, the SAP HANA .NET provider is not covered by the MIT license and is not redistributed in this repository.

