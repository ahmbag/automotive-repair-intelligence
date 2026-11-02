# Sprint 01 MVP scope

## Reference case

Sprint 01 proves one end-to-end category: control-arm replacement,
`SUSPENSION_CONTROL_ARM_REPLACE`. Training data is synthetic and reproducible.

## Included in Sprint 01

- One repair category as a complete reference flow
- Synthetic workshop orders generated with a fixed seed and versioned logic
- SAP HANA Cloud as primary planned persistence
- Statistical reference-time calculation
- A trained, versioned Python regression model with basic metrics
- LLM-assisted information extraction
- A complete REST estimation pipeline
- Reproducible automated tests and a Swagger/OpenAPI or HTTP-client demonstration

## Explicitly excluded

- Full workshop administration and customer/vehicle master-data integration
- Billing, inventory, and appointment scheduling
- Real customer data
- Full RAG and vector search (planned for Sprint 02)
- SAP PAL
- Production model monitoring
- A large Blazor UI
- Multi-category prediction before the reference flow is proven

## MVP definition of done

Given vehicle facts and a free-text repair description, the system:

1. extracts relevant normalized information while retaining its source,
2. validates prediction-time inputs,
3. obtains a numerical prediction from a versioned ML model,
4. presents the conventional baseline alongside it, and
5. returns and stores model version, timestamp, calculation strategy, and data origin.

A full UI is not required. OpenAPI or a small HTTP demonstration is sufficient.

## Foundation Day acceptance (ARI-FOUNDATION-01)

- [x] Repository layout and `.slnx` solution exist.
- [x] `ARI.Api`, `ARI.Core`, and `ARI.Infrastructure` have the intended references.
- [x] Python training, inference, evaluation, and test package structure is prepared.
- [x] The six domain objects and their temporal responsibilities are defined.
- [x] Estimate request/response and ML/LLM boundary contracts are code-defined and documented.
- [x] Architecture, dependency direction, data flow, and integration limits are documented.
- [x] README, MVP scope, API contract, and ADR 0001 exist.
- [x] .NET build and foundation tests are automated.
- [x] `GET /health` is mapped and covered by an API test.
- [x] Repository configuration contains no HANA, LLM, or ML credentials.

Executable evidence is produced with `dotnet build`, `dotnet test`, and a live HTTP call as shown
in the root README. Python execution requires a Python 3.11+ installation.
