# ARI-RC1 reproducibility and CI

This guide separates an infrastructure-free verification path from the explicitly opt-in live stack. The first path is the public release gate and never calls HANA or OpenAI. The second path proves the integrated system but requires private infrastructure and may incur provider cost.

## Frozen toolchain and dependencies

| Boundary | Release-candidate contract |
| --- | --- |
| .NET SDK | `10.0.401`, selected by [`global.json`](../global.json); later 10.0 patch releases are permitted |
| NuGet | exact direct and transitive resolution in the 15 project-level `packages.lock.json` files |
| Python | CPython `3.14.6`, selected by [`.python-version`](../.python-version) |
| Python packages | exact runtime/test resolution in [`ml/requirements.lock`](../ml/requirements.lock) |
| ML model | `ari-repair-duration-v1`; checksum verified before deserialization |
| Browser | Playwright .NET `1.55.0`, Chromium installed by the Playwright project script |

`ml/pyproject.toml` remains the human-maintained compatibility range. The lock file is the release-candidate resolution. NuGet locked mode and pip's exact pins make dependency drift visible instead of silently accepting a new transitive version.

## Stage 1: fresh checkout without live infrastructure

The initial package download needs access to NuGet and PyPI. Once restored, the tests below need no HANA instance, OpenAI key, or Python model binary.

```powershell
git clone <repository-url>
Set-Location "Automotive Repair Intelligence"

dotnet --version                     # 10.0.401 or a later 10.0 patch
python --version                     # Python 3.14.6

dotnet restore AutomotiveRepairIntelligence.slnx --locked-mode
dotnet build AutomotiveRepairIntelligence.slnx --configuration Release --no-restore

python -m venv .venv
.venv\Scripts\python.exe -m pip install --requirement ml/requirements.lock
.venv\Scripts\python.exe -m pip install --no-build-isolation --no-deps --editable ml

& .\tools\validate-release.ps1 -SkipRestore
```

On Linux/macOS use `.venv/bin/python` and run the PowerShell script with `pwsh`. The script performs:

1. a warning-as-error Release build;
2. 18 Core, 58 API, and 9 Web tests;
3. 21 model-independent Python contract, generation, training, and inference tests;
4. 15 fixture-backed Playwright tests;
5. the publication/security scan over current files and the complete Git history.

All 121 executed tests are deterministic and require no unpublished artifact. The three `TrainedArtifactAcceptanceTests` are deliberately part of Stage 2 because `model.joblib` is excluded from Git; they must pass against the checksum-verified local model before the final live browser run. `Category=HanaIntegration`, `Category=DataAcceptance`, `Category=Live`, and `Category=LiveCapture` are not part of Stage 1.

## GitHub Actions

[`ci.yml`](../.github/workflows/ci.yml) runs on every push and pull request and can also be started manually. Its four independent jobs are:

| Job | Work performed | External runtime calls |
| --- | --- | --- |
| `.NET build and deterministic tests` | locked restore, Release build, Core/API/Web tests | none |
| `Python contracts and ML tests` | locked install, 21 model-independent tests; three artifact tests explicitly deselected | none |
| `Deterministic browser acceptance` | Chromium plus the visible fixture provider | none |
| `Publication hygiene` | current files and full Git-history secret scan | none |

Test results are uploaded as CI artifacts. Workflow permissions are read-only. No secrets are declared, and ordinary CI can never call HANA or OpenAI. Live validation is deliberately not an automatic push/PR job.

## Configuration without committed secrets

[`.env.example`](../.env.example) is a key inventory, not an automatically loaded configuration file. Empty values are intentional. Use .NET User Secrets for `ARI.Api` during local development or process environment variables for command-line tools. Do not pass passwords as command arguments or commit a populated `.env` file.

The live API needs:

- `Hana:{Host,Port,User,Password}` with the restricted `ARI_APP` runtime user;
- `Llm:{ApiKey,Model}`;
- `Embeddings:ApiKey` (the model/profile defaults are versioned in `appsettings.json`);
- `MachineLearning:BaseAddress=http://127.0.0.1:8100/`.

`ARI.Web` needs `AriApi:BaseUrl=http://127.0.0.1:5012` and `AriApi:UseFixture=false`. Environment-variable names replace `:` with `__`.

## Stage 2: prepare the live stack

### 1. Generate the deterministic source data

```powershell
$env:PYTHONPATH = (Resolve-Path "ml/src").Path
.venv\Scripts\python.exe -m ari_ml.synthetic.generator `
  --count 1000 --seed 20261023 `
  --dataset-id ari-suspension-synthetic-v1 `
  --output artifacts/ari-suspension-synthetic-v1.json
```

Expected canonical record checksum: `960588c090ad9092dc98f59721e08ad8df3718d899012ace0b344fb238ff1384`.

### 2. Migrate, import, and index HANA

Run migration only with `ARI_DEPLOY`; then switch to `ARI_APP` for every runtime/data command. Credentials remain process-local and are omitted here.

```powershell
dotnet run --project tools/ARI.DatabaseMigrator -- database/hana
dotnet run --project tools/ARI.HistoricalDataImporter -- artifacts/ari-suspension-synthetic-v1.json 100
dotnet run --project tools/ARI.RagTool -- index-v2 artifacts/ari-suspension-synthetic-v1.json
```

The import and V2 index are idempotent. The first execution imports 1,000 cases and creates 2,000 V2 documents; an unchanged repeat reports no new import/embedding work.

### 3. Rebuild the model when the local binary is absent

The binary `model.joblib` is intentionally not published. Export with `ARI_APP`, then train locally:

```powershell
dotnet run --project tools/ARI.MlDatasetExporter -- `
  ari-suspension-synthetic-v1 artifacts/ml/training-export.json

.venv\Scripts\python.exe -m ari_ml.training.train `
  --config ml/config/training-v1.toml `
  --dataset artifacts/ml/training-export.json `
  --output-dir artifacts/ml/ari-repair-duration-v1
```

Expected model SHA-256: `d206f94f7c532d23c210ca0300b978ce5253786143ca4f67c0c8b6a3d8b06840`.

### 4. Start all three processes

```powershell
# terminal 1
$env:ARI_ML_ARTIFACT_DIR = "$PWD\artifacts\ml\ari-repair-duration-v1"
.venv\Scripts\python.exe -m ari_ml.inference.api

# terminal 2 (Development loads ARI.Api User Secrets)
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:MachineLearning__BaseAddress = "http://127.0.0.1:8100/"
dotnet run --project src/ARI.Api --no-launch-profile --urls http://127.0.0.1:5012

# terminal 3
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:AriApi__BaseUrl = "http://127.0.0.1:5012"
$env:AriApi__UseFixture = "false"
dotnet run --project src/ARI.Web --no-launch-profile --urls http://127.0.0.1:5200
```

Readiness gates are `http://127.0.0.1:8100/health/ready`, `http://127.0.0.1:5012/health/ready`, `http://127.0.0.1:5012/health/ml/ready`, and `http://127.0.0.1:5200/health-ui`.

### 5. Run the opt-in integration acceptance

HANA tests read `Hana__*` process variables. Set `ARI_DATASET_ID=ari-suspension-synthetic-v1`, `ARI_EXPECT_RAG_INDEX=1`, and `ARI_EXPECT_RAG_V2_INDEX=1` when verifying the prepared dataset/index.

```powershell
dotnet test tests/ARI.Infrastructure.IntegrationTests `
  --configuration Release `
  --filter "Category=HanaIntegration|Category=DataAcceptance"

$env:ARI_LIVE_UI_URL = "http://127.0.0.1:5200"
dotnet test tests/ARI.Web.E2ETests `
  --configuration Release `
  --filter "Category=Live"
```

The live browser test rejects the fixture banner, submits the synthetic case, requires accepted LLM extraction, real Ridge inference, five HANA RRF V2 sources, grounded evidence, two what-if results, and an unchanged baseline.

Before it, verify the three tests that require the intentionally unpublished model binary:

```powershell
.venv\Scripts\python.exe -m pytest ml/tests/test_inference_api.py `
  -k "TrainedArtifactAcceptanceTests"
```

## Evidence and limitations

The fresh release-candidate result is recorded in [01F acceptance](showcase-01f-acceptance.md). HANA Free Tier may be stopped and OpenAI credentials may expire, so the public portfolio remains understandable through the deterministic path, frozen evaluation artifacts, and live screenshots. A prior green report is never substituted for a current live run; unavailable live dependencies must be reported as such.
