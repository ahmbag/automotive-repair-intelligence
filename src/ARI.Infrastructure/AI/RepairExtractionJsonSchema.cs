using System.Text.Json.Nodes;

namespace ARI.Infrastructure.AI;

/// <summary>
/// Strict provider schema. Every property is required; semantically optional scalars use null.
/// Domain validation is deliberately separate from this structural contract.
/// </summary>
internal static class RepairExtractionJsonSchema
{
    public static JsonNode Create() => JsonNode.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "documentType": {
              "type": "string",
              "enum": ["PRE_REPAIR_DESCRIPTION", "REPAIR_COMPLETION_REPORT"]
            },
            "vehicle": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "manufacturer": { "type": ["string", "null"] },
                "model": { "type": ["string", "null"] },
                "modelYear": { "type": ["integer", "null"] },
                "mileageKm": { "type": ["integer", "null"] }
              },
              "required": ["manufacturer", "model", "modelYear", "mileageKm"]
            },
            "repairCode": {
              "anyOf": [
                {
                  "type": "string",
                  "enum": [
                    "CONTROL_ARM_FRONT_LEFT_REPLACE",
                    "CONTROL_ARM_FRONT_PAIR_REPLACE",
                    "CONTROL_ARM_FRONT_RIGHT_REPLACE",
                    "CONTROL_ARM_REAR_LEFT_REPLACE",
                    "CONTROL_ARM_REAR_RIGHT_REPLACE"
                  ]
                },
                { "type": "null" }
              ]
            },
            "observedComplications": {
              "type": "array",
              "items": { "$ref": "#/$defs/complication" }
            },
            "knownBeforeRepair": {
              "type": "array",
              "items": { "$ref": "#/$defs/knownFact" }
            },
            "actualLaborMinutes": { "type": ["integer", "null"] },
            "status": { "type": "string", "enum": ["EXTRACTED", "NEEDS_REVIEW"] }
          },
          "required": [
            "documentType", "vehicle", "repairCode", "observedComplications",
            "knownBeforeRepair", "actualLaborMinutes", "status"
          ],
          "$defs": {
            "complication": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "code": {
                  "type": "string",
                  "enum": ["SEIZED_FASTENER", "BROKEN_BOLT", "DAMAGED_BUSHING_SEAT", "ALIGNMENT_CORRECTION"]
                },
                "informationTime": {
                  "type": "string",
                  "enum": ["DURING_REPAIR", "AFTER_REPAIR", "UNKNOWN"]
                },
                "evidence": { "type": "string" }
              },
              "required": ["code", "informationTime", "evidence"]
            },
            "knownFact": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "code": { "type": "string", "enum": ["VISIBLE_CORROSION", "VISIBLE_PRIOR_DAMAGE"] },
                "informationTime": { "type": "string", "enum": ["BEFORE_REPAIR"] },
                "evidence": { "type": "string" }
              },
              "required": ["code", "informationTime", "evidence"]
            }
          }
        }
        """)!;
}
