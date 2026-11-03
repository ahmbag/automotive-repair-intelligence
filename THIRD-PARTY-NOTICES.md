# Third-party notices

This file records the dependency inventory reviewed for `ARI-RC1` on 2026-11-03. Package authors retain all rights in their software. The repository's MIT license applies only to ARI-owned code and documentation.

## SAP HANA provider

| Package | Version | License status |
| --- | ---: | --- |
| `Sap.Data.Hana.Net.v10.0` | 2.30.27 | SAP Developer License 3.2, license acceptance required |

The provider is referenced by NuGet name/version only. No SAP DLL, native runtime, NuGet archive, license file, or SDK binary is committed. Each developer obtains the package from its official NuGet distribution and is responsible for accepting and complying with SAP's terms. The ARI MIT license does not relicense SAP software, SAP HANA Cloud, or SAP trademarks.

## .NET packages

The offline `project.assets.json` inventory contains these license families:

| Package family | Reviewed versions | License |
| --- | --- | --- |
| Microsoft ASP.NET Core / Extensions / TestPlatform | 10.0.0, internal assets 10.0.12, Test SDK 17.14.1 | MIT |
| Microsoft Playwright, adapter and xUnit integration | 1.55.0 | MIT |
| Coverlet collector | 6.0.4 | MIT |
| Newtonsoft.Json | 13.0.3 | MIT |
| xUnit, runner, analyzers, assert/core/extensibility | 2.9.3 / runner 3.1.4 / abstractions 2.0.3 | Apache-2.0 |
| System support packages | versions resolved in `project.assets.json` | MIT |

These packages are restored from NuGet and are not copied into Git.

## Python packages

The audited virtual environment resolved the following runtime/test dependency set. The environment itself is ignored and is not redistributed.

| Package | Version | License expression/metadata |
| --- | ---: | --- |
| annotated-doc | 0.0.5 | MIT |
| annotated-types | 0.8.0 | MIT |
| anyio | 4.15.1 | MIT |
| certifi | 2026.10.23 | MPL-2.0 |
| click | 8.5.0 | BSD-3-Clause |
| cloudpickle | 3.1.2 | BSD-3-Clause |
| colorama | 0.4.6 | BSD-3-Clause (package metadata omits the expression) |
| fastapi | 0.162.0 | MIT |
| h11 | 0.16.0 | MIT |
| httpcore | 1.0.9 | BSD-3-Clause |
| httpx | 0.28.1 | BSD-3-Clause |
| idna | 3.20 | BSD-3-Clause |
| iniconfig | 2.3.1 | MIT |
| joblib | 1.6.0 | BSD-3-Clause |
| narwhals | 2.27.1 | MIT |
| numpy | 2.5.4 | BSD-3-Clause plus bundled 0BSD/MIT/Zlib/CC0 notices |
| opentelemetry-api | 1.45.1 | Apache-2.0 |
| packaging | 26.3 | Apache-2.0 OR BSD-2-Clause |
| pandas | 3.1.0 | BSD-3-Clause |
| pluggy | 1.7.0 | MIT |
| pydantic / pydantic-core | 2.14.1 / 2.50.1 | MIT |
| Pygments | 2.21.0 | BSD-2-Clause |
| pytest | 9.2.0 | MIT |
| python-dateutil | 3.0.0 | dual BSD/Apache-style terms in distribution metadata |
| scikit-learn | 1.9.1 | BSD-3-Clause |
| scipy | 1.18.1 | BSD-3-Clause plus bundled component notices |
| six | 1.17.0 | MIT |
| starlette | 1.10.0 | BSD-3-Clause |
| threadpoolctl | 3.7.0 | BSD-3-Clause |
| typing_extensions | 4.16.0 | PSF-2.0 |
| typing-inspection | 0.4.4 | MIT |
| tzdata | 2026.5 | Apache-2.0 |
| uvicorn | 0.54.0 | BSD-3-Clause |

`pip`, build tools, and their licenses are development-environment tooling rather than ARI runtime dependencies. Generated model/data artifacts remain subject to their source-data and library terms; ARI-RC1 uses only synthetic source data.

## External services and assets

- OpenAI APIs and models are accessed over HTTPS; no OpenAI SDK or model weights are redistributed.
- SAP HANA Cloud is an external service; no database software is redistributed.
- Google Fonts CSS is referenced at runtime for Manrope and DM Mono. No font binary is committed; service terms and privacy behavior remain external to this repository.
- Product and company names are descriptive references and do not imply endorsement.

## Audit method

NuGet licenses were read from locally restored `.nuspec` files and all unique packages in `project.assets.json`. Python licenses were read from installed `*.dist-info/METADATA`. An `UNKNOWN` or missing expression was resolved only where the package distribution itself contained an unambiguous license file; no third-party code was relicensed.

