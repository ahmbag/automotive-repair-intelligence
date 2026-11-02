"""FastAPI model-serving process. Training is intentionally not exposed here."""

from __future__ import annotations

import os
from pathlib import Path

import uvicorn
from fastapi import FastAPI, HTTPException, status

from ari_ml.datasets.features import FEATURE_SCHEMA_VERSION
from ari_ml.inference.predictor import ModelPredictor
from ari_ml.inference.schemas import (
    PredictionRequest,
    PredictionResponse,
    ReadinessResponse,
)


def create_app(artifact_dir: Path | None = None) -> FastAPI:
    app = FastAPI(title="ARI ML Inference", version="1.0.0")
    configured_dir = artifact_dir or Path(
        os.environ.get("ARI_ML_ARTIFACT_DIR", "artifacts/ml/ari-repair-duration-v1")
    )
    try:
        app.state.predictor = ModelPredictor(configured_dir)
        app.state.load_error = None
    except Exception as exception:  # readiness reports controlled failure; health stays alive
        app.state.predictor = None
        app.state.load_error = f"{type(exception).__name__}: {exception}"

    @app.get("/health")
    def health() -> dict[str, str]:
        return {"status": "Healthy", "service": "ARI.ML"}

    @app.get(
        "/health/ready",
        response_model=ReadinessResponse,
        responses={503: {"model": ReadinessResponse}},
    )
    def readiness():
        predictor = app.state.predictor
        if predictor is None:
            return _not_ready()
        return ReadinessResponse(
            status="Ready",
            modelVersion=predictor.model_version,
            featureSchemaVersion=FEATURE_SCHEMA_VERSION,
        )

    @app.post("/predict", response_model=PredictionResponse)
    def predict(request: PredictionRequest) -> PredictionResponse:
        predictor = app.state.predictor
        if predictor is None:
            raise HTTPException(
                status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
                detail="model artifact is not ready",
            )
        try:
            return predictor.predict(request)
        except ValueError as exception:
            raise HTTPException(
                status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
                detail=str(exception),
            ) from exception

    def _not_ready():
        from fastapi.responses import JSONResponse

        payload = ReadinessResponse(
            status="NotReady", modelVersion=None, featureSchemaVersion=None
        )
        return JSONResponse(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            content=payload.model_dump(by_alias=True),
        )

    return app


app = create_app()


def main() -> None:
    uvicorn.run(
        "ari_ml.inference.api:app",
        host=os.environ.get("ARI_ML_HOST", "127.0.0.1"),
        port=int(os.environ.get("ARI_ML_PORT", "8100")),
        reload=False,
    )


if __name__ == "__main__":
    main()
