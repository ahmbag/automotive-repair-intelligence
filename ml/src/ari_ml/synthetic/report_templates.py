"""Natural-language report rendering with deterministic variation."""

NORMAL_REPORTS = (
    "The scheduled suspension operation was completed without unusual findings.",
    "Repair completed as planned. No additional dismantling work was required.",
    "The control-arm replacement followed the standard procedure and final checks passed.",
)

EVENT_PHRASES = {
    "SEIZED_FASTENER": (
        "Corroded mounting hardware required additional extraction work.",
        "One or more fasteners resisted removal and needed careful release.",
        "Dismantling took longer because mounting hardware was seized.",
    ),
    "BROKEN_BOLT": (
        "A mounting bolt broke during removal and was replaced.",
        "Bolt extraction and replacement were necessary during dismantling.",
        "A failed fastener added an unplanned removal step.",
    ),
    "DAMAGED_BUSHING_SEAT": (
        "The bushing seat required cleaning and corrective preparation.",
        "Additional work was carried out around the worn bushing mounting area.",
        "The mounting seat needed attention before the new component could be installed.",
    ),
    "ALIGNMENT_CORRECTION": (
        "Final alignment required an additional correction.",
        "A follow-up adjustment was made during the geometry check.",
        "Wheel geometry needed further correction after assembly.",
    ),
}


def render_report(rng, complication_codes: list[str], vague: bool) -> str:
    """Render a report without copying the structured event list mechanically."""

    if not complication_codes:
        return rng.choice(NORMAL_REPORTS)

    phrases = [rng.choice(EVENT_PHRASES[code]) for code in complication_codes]
    if vague and len(phrases) > 1:
        phrases = phrases[:-1]
        phrases.append("Further unplanned work was documented during completion.")
    return " ".join(phrases)
