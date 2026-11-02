# ADR 0001: Modular .NET monolith with separate Python ML service

- Status: Accepted
- Date: 2026-10-22

## Context

ARI needs a small operational application, SAP HANA persistence, language-model-assisted feature
extraction, and independently reproducible ML training/inference. Splitting every feature into a
service would increase deployment and integration work before the first prediction flow is proven.
Embedding Python training into the .NET process would couple distinct runtimes and release cycles.

## Decision

Use a modular .NET 10 monolith consisting of `ARI.Api`, `ARI.Core`, and `ARI.Infrastructure`.
Keep Python ML as a separately deployable component and communicate over REST/JSON. SAP HANA is
the planned system of record and may be accessed only by Infrastructure. LLMs are hidden behind a
Core feature-extraction port, with a future provider adapter based on `Microsoft.Extensions.AI`.

Dependencies point inward: Core is vendor-neutral; Infrastructure references Core; API composes
Infrastructure and Core. Training is offline from the operational request flow. LLM extraction and
ML prediction remain separate responsibilities.

## Consequences

Benefits are low initial operational overhead, testable boundaries, replaceable providers, clear
ownership of numerical prediction, and independent Python model development. Costs are an explicit
HTTP contract and deployment of a second runtime. The monolith must keep module boundaries clean to
avoid becoming a single unstructured codebase.

Vector search, RAG, SAP PAL, production monitoring, and user-interface expansion are deferred.
