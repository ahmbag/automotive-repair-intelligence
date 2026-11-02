"""Online inference contracts and, later, the REST application."""

from .contracts import LaborPredictionInput, LaborPredictionResult
from .predictor import ModelPredictor

__all__ = ["LaborPredictionInput", "LaborPredictionResult", "ModelPredictor"]
