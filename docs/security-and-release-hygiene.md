# ARI-SHOWCASE-01E security, licensing, and repository hygiene

Audit date: 2026-11-03  
Release line: `ARI-RC1`  
Publication target: source repository and static evidence, not an unauthenticated live service

## Secret and history review

The current tree and every reachable commit were checked. At audit time the repository had one root commit, so the complete history was covered rather than only the working tree.

| Area | Result | Evidence |
| --- | --- | --- |
| `appsettings*.json` | Pass | API keys are `null`; HANA credentials/connection strings are absent; Web contains only loopback API defaults |
| .NET User Secrets | Pass | only `UserSecretsId` is tracked; the user-profile secret store is outside Git |
| Python `.env` | Pass | `.env` and `.env.*` are ignored, except an optional future `.env.example` |
| Git history | Pass | all commits scanned for OpenAI-style keys, private-key blocks, and long bearer credentials |
| Logs/test reports | Pass | no tracked log/TestResults/report output; versioned evaluation JSON contains metrics/case evidence, not auth headers |
| Screenshots | Pass | all four visually reviewed; synthetic cases and opaque synthetic IDs only, no endpoint/credential/path disclosure |
| CI configuration | Pass | no CI file exists yet; therefore no hard-coded CI secret is present (CI belongs to 01F) |
| Build/local artifacts | Pass | no tracked `bin`, `obj`, `.venv`, cache, DLL, executable, PDB, `.env`, local appsettings, or secret file |

The repeatable [`security-audit.ps1`](../tools/security-audit.ps1) fails on forbidden tracked paths, non-empty sensitive appsettings values, and known credential patterns in any reachable commit. It suppresses candidate values in its own diagnostics.

The broad keyword review produced only configuration property names, placeholders, environment-variable reads, security documentation, token-count telemetry, and explicit dummy values inside isolated tests. No releasable credential was found.

## Configuration and error handling

- HANA, LLM, and embedding credentials enter `ARI.Api` through User Secrets/environment and never reach `ARI.Web` or the browser.
- HANA connections always request TLS with server-certificate validation.
- Browser → API is the only UI integration path; browser → HANA/OpenAI is absent.
- Core and endpoint validators constrain text length, numeric ranges, enum/version combinations, temporal boundaries, evidence IDs, and source claims.
- Provider/database failures are translated to controlled status codes and sanitized messages. Raw connection strings, credentials, response bodies, and stack traces are not returned by API endpoints.
- Health endpoints expose state/version/user identity needed for a local demo, not secrets. They are diagnostics and must not be internet-exposed without access control.

## Cost and public-deployment boundary

Extraction, embeddings, and grounded explanation can cause OpenAI cost. ARI-RC1 has per-request validation, timeout/retry limits, one active analysis per UI circuit, and explicit cancellation, but it does **not** have user authentication, authorization, quotas, or distributed rate limiting.

Therefore the approved publication is repository + documentation + screenshots + deterministic fixture demo. A live internet deployment is blocked until the controls in [`SECURITY.md`](../SECURITY.md) are implemented. This converts a potentially ambiguous risk into an explicit release boundary.

## Data classification

Committed datasets, evaluation cases, screenshots, vehicle identifiers, reports, and repair-order references are synthetic. No customer name, address, contact data, VIN, registration number, employee data, or real workshop record is present or permitted in ARI-RC1.

Correlation IDs and repair UUIDs visible in screenshots are opaque identifiers from a synthetic demonstration. They are not credentials and do not grant access.

## License decision

ARI-owned code and documentation are released under the [MIT License](../LICENSE). MIT was selected for a small portfolio/learning project because it is short, permissive, and easy for reviewers to understand. It includes the standard warranty/liability disclaimer but does not add Apache-2.0's explicit patent-license language.

This choice does not change third-party licenses. The complete reviewed inventory and the proprietary SAP boundary are documented in [`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md).

## Dependency vulnerability review

Both ecosystem scans were executed against the resolved 2026-11-03 environment:

- `dotnet list AutomotiveRepairIntelligence.slnx package --vulnerable --include-transitive --no-restore` reported no known vulnerable package in any of the 15 projects;
- `pip-audit --local` reported no known vulnerability. The local `ari-ml` package was correctly skipped because it is repository code rather than a package published on PyPI.

These results are time-bound, not a permanent guarantee. A later release must resolve dependencies again and repeat both scans.

## Publication rights

- ARI code, documentation, diagrams, prompts, synthetic fixtures, and generated screenshots were created for this repository and are covered by its MIT notice.
- Synthetic vehicle/manufacturer/model names are factual demo descriptors; no logos or copied marketing assets are included.
- No SAP/OpenAI model weights, SDK archives, DLLs, database software, or proprietary documentation is redistributed.
- The SAP provider is obtained independently through NuGet subject to its own license acceptance.
- External Google Fonts are fetched at runtime rather than copied into the repository; a privacy-sensitive deployment must self-host approved assets or remove the import.

No unresolved redistribution right was found for the content intended to be committed and published.

## Residual risks

1. Keys previously pasted into chat are outside Git but should be considered disclosed and revoked/rotated.
2. A public live service still requires authentication, authorization, rate/budget controls, tenant isolation, deployment hardening, and an approved privacy model.
3. Dependency licenses and vulnerability status must be refreshed before each later release because resolved package versions can change.
4. GitHub secret scanning and push protection should be enabled when the repository is created; local scanning is defense in depth, not a guarantee.
