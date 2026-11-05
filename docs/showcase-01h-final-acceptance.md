# ARI-SHOWCASE-01H final acceptance record

| Item | Accepted value |
| --- | --- |
| Acceptance date | 2026-11-05 |
| Decision | **PASS — accepted for public portfolio publication** |
| Release | [`v0.1.0-rc.4`](https://github.com/ahmbag/automotive-repair-intelligence/releases/tag/v0.1.0-rc.4) |
| Release commit | [`57ee40b0509de9bca2799f4cc13a1e2c95671dab`](https://github.com/ahmbag/automotive-repair-intelligence/commit/57ee40b0509de9bca2799f4cc13a1e2c95671dab) |
| Annotated tag object | `4f9e945b889fe5546bd09356b8b61448e5d0188d` |

## Attestation boundary

This record is a post-release attestation of the immutable RC4 tag. Its own
documentation commit is intentionally not part of the RC4 tree and does not move,
replace, or recreate the published tag. All acceptance statements below refer to
the exact tree identified by release commit
`57ee40b0509de9bca2799f4cc13a1e2c95671dab`.

The repository, release, source evidence, and screenshots contain synthetic data
only. This is a learning and portfolio release candidate, not a production,
safety, pricing, or repair-authorization system.

## 1. Release integrity

| Integrity control | Result | Evidence |
| --- | --- | --- |
| Release tag is annotated | PASS | Git object `4f9e945b889fe5546bd09356b8b61448e5d0188d` has type `tag` |
| Tag resolves to the accepted commit | PASS | Peeled target is exactly `57ee40b0509de9bca2799f4cc13a1e2c95671dab` |
| Version is internally consistent | PASS | `VERSION` in the tagged tree contains `0.1.0-rc.4` |
| Published commit is unchanged | PASS | Before this post-release record, local `HEAD`, `origin/main`, and the peeled tag target all resolved to the accepted commit; `git diff v0.1.0-rc.4^{}` was empty |
| Release tree is publication-safe | PASS | Repeatable audit scanned all 302 tracked files and all 8 commits reachable at RC4; no secret, private key, forbidden binary/build output, local configuration, or proprietary SAP library was found |
| No unverified change is attributed to RC4 | PASS | Later documentation belongs to `main`, not to the immutable RC4 tree; RC4 test and release claims remain scoped to the tagged commit |

The fixes between the earlier candidates and RC4 were limited to reproducible
toolchain/runner selection, cross-platform browser-test semantics, and release
metadata. The accepted application behavior is the behavior exercised by the
documented live regression. Earlier RC tags remain visible as transparent
candidate history and are superseded by RC4.

## 2. Final acceptance evidence

### Test inventory

| Evidence group | Passed | Scope |
| --- | ---: | --- |
| Deterministic public-checkout suite | 121/121 | 18 Core, 58 API, 9 Web, 21 model-independent Python, and 15 deterministic browser tests |
| Trained-artifact acceptance | 3/3 | Checksum-verified local Ridge artifact deliberately excluded from Git |
| HANA integration and data acceptance | 16/16 | Authenticated TLS connectivity, persistence, data, and retrieval gates; no skips |
| Live browser acceptance | 1/1 | Complete UI → API → OpenAI/Python/HANA path; no skip |
| **Final documented acceptance** | **141/141** | All deterministic and explicitly authorized live gates |

The exact environment, commands, readiness results, provider evidence, and test
breakdown are preserved in the
[`ARI-SHOWCASE-01F` acceptance record](showcase-01f-acceptance.md). No unavailable
or skipped live test is counted as a pass.

### Security, licensing, and publication hygiene

| Control | Result |
| --- | --- |
| Full tracked tree and reachable history secret scan | PASS |
| Forbidden build output, local config, model binary, and credential scan | PASS |
| .NET resolved dependency vulnerability audit | PASS — no known vulnerable package at audit time |
| Python resolved dependency vulnerability audit | PASS — no known vulnerability at audit time |
| MIT license and third-party notices | PASS |
| Proprietary SAP provider redistribution boundary | PASS — provider binaries are not tracked or attached |
| Four public screenshots, relative links, Mermaid diagrams, and documentation links | PASS |

The detailed, repeatable review and its time-bound caveats are recorded in
[`ARI-SHOWCASE-01E security, licensing, and repository hygiene`](security-and-release-hygiene.md).

### Public CI

Two independent public GitHub Actions executions validated the accepted release:

| Run | Trigger | Result | Jobs |
| --- | --- | --- | --- |
| [`main` run #7](https://github.com/ahmbag/automotive-repair-intelligence/actions/runs/42032430829) | Push of accepted RC4 commit | PASS | .NET, Python, Browser Acceptance, Publication Hygiene |
| [`v0.1.0-rc.4` run #8](https://github.com/ahmbag/automotive-repair-intelligence/actions/runs/42032829747) | Published release tag | PASS | .NET, Python, Browser Acceptance, Publication Hygiene |

All eight job executions completed successfully. Normal CI uses read-only
permissions, contains no HANA/OpenAI credentials, and does not represent a live
provider test. The live-provider evidence is instead the separately authorized
and documented 20-test extension to the 121-test public-checkout suite.

## 3. Known limitations

The following are explicit release limitations, not hidden quality claims:

1. Training, evaluation, screenshots, and demonstrations use synthetic repair
   data. The candidate has not been independently validated against real workshop
   populations, workflows, data quality, or outcome distributions.
2. The ML result is a point estimate without a calibrated uncertainty interval.
   Performance on the controlled synthetic holdout does not establish real-world
   generalization, especially for rare or unusually expensive repairs.
3. LLM extraction was evaluated against a bounded schema and controlled cases.
   Provider behavior, model versions, latency, cost, and extraction quality can
   change; numerical effort prediction remains owned by the Ridge model.
4. Retrieval evidence demonstrates the versioned 1,000-case synthetic corpus and
   the documented HANA Vector Engine path, not production-scale concurrency,
   tenancy, load, or relevance on real workshop language.
5. What-if differences are model sensitivity demonstrations, not causal effects,
   diagnoses, quotations, commitments, or repair authorization.
6. A public live service is outside this release boundary. Authentication,
   authorization, tenant isolation, quotas/budget controls, privacy approval,
   observability, operational hardening, and incident response remain required
   before any such deployment.
7. Live HANA/OpenAI use requires separately licensed/configured services and can
   incur cost. Credentials are never included in the repository or release.

Real-workshop validation is therefore a named future validation activity. Its
absence does not block publication of an accurately scoped synthetic portfolio
candidate, but it does block any claim of production readiness or real-world
predictive validity.

## 4. Release-blocker decision

| Potential blocker | Decision |
| --- | --- |
| Release/tag does not identify the accepted source | CLEARED |
| Uncommitted or untested source included in RC4 | CLEARED |
| Deterministic or live acceptance failure | CLEARED |
| Public CI failure | CLEARED |
| Credential, personal-data, or proprietary-binary exposure | CLEARED |
| Missing license or publication rights | CLEARED |
| Broken public screenshots/diagrams/documentation links | CLEARED |
| Limitations presented as production evidence | CLEARED — limitations are explicit |

**Open publication blockers: 0.**

## Final decision

`v0.1.0-rc.4` is formally accepted as the final ARI portfolio release candidate.
Its full source identity, 141-test acceptance evidence, security and licensing
audit, two successful public CI executions, and material limitations are
documented and mutually consistent. No known blocker remains within the declared
repository-and-documentation publication scope.
