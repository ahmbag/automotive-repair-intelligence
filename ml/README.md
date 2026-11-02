# ARI ML

Independent Python 3.11+ component for reproducible training, evaluation, and read-only inference.

```powershell
python -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e ".[test]"
python -m unittest discover -s tests -v

python -m ari_ml.training.train `
  --config config/training-v1.toml `
  --dataset ../artifacts/ml/training-export.json `
  --output-dir ../artifacts/ml/ari-repair-duration-v1

$env:ARI_ML_ARTIFACT_DIR = "$PWD\..\artifacts\ml\ari-repair-duration-v1"
python -m ari_ml.inference.api
```

Rules:

- synthetic generation starts with `RANDOM_SEED = 1729` and `GENERATOR_VERSION = "synthetic-v1"`;
- prediction inputs contain only information available before the repair;
- every result identifies the model version and data source;
- evaluation code is kept separate from serving code;
- `/health` is liveness, `/health/ready` verifies the loaded model, and `/predict` serves it;
- the service deliberately has no training endpoint;
- only checksum-verified, locally produced joblib artifacts are trusted.
