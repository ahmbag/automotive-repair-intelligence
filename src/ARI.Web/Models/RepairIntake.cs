using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;

namespace ARI.Web.Models;

public sealed class RepairIntake : IValidatableObject
{
    [StringLength(80)] public string? Manufacturer { get; set; }
    [StringLength(80)] public string? Model { get; set; }

    [Range(1950, 2100, ErrorMessage = "Model year must be between 1950 and 2100.")]
    public int? ModelYear { get; set; }

    [Range(0, 5_000_000, ErrorMessage = "Mileage must be between 0 and 5,000,000 km.")]
    public int? MileageKm { get; set; }

    [Required(ErrorMessage = "Select a supported repair operation.")]
    public string RepairCode { get; set; } = RepairCatalog.Codes[0];

    [Required, StringLength(4000, MinimumLength = 20,
        ErrorMessage = "Describe the pre-repair condition in 20 to 4,000 characters.")]
    public string Description { get; set; } = string.Empty;

    [Range(0.1, 100.0, ErrorMessage = "Baseline labor must be between 0.1 and 100 hours.")]
    public decimal BaselineLaborHours { get; set; } = 2.2m;

    public bool VisibleCorrosion { get; set; }
    public bool VisiblePriorDamage { get; set; }
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow.Date;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AsOfDate.Date > DateTime.UtcNow.Date.AddDays(1))
            yield return new("As-of date cannot be in the future.", [nameof(AsOfDate)]);
        if (ModelYear is not null && ModelYear > AsOfDate.Year + 1)
            yield return new("Model year is implausible for the selected as-of date.", [nameof(ModelYear)]);
        foreach (var conflict in IntakeConflictDetector.Find(this))
            yield return new(conflict, [nameof(Description)]);
    }

    public string BuildAnalysisDocument()
    {
        var text = new StringBuilder();
        text.AppendLine("Trusted structured pre-repair intake:");
        if (!string.IsNullOrWhiteSpace(Manufacturer)) text.AppendLine($"Manufacturer: {Manufacturer.Trim()}");
        if (!string.IsNullOrWhiteSpace(Model)) text.AppendLine($"Model: {Model.Trim()}");
        if (ModelYear is not null) text.AppendLine($"Model year: {ModelYear}");
        if (MileageKm is not null) text.AppendLine($"Mileage: {MileageKm} km");
        text.AppendLine($"Requested repair code: {RepairCode}");
        if (VisibleCorrosion) text.AppendLine("Known before repair: visible corrosion.");
        if (VisiblePriorDamage) text.AppendLine("Known before repair: visible prior damage.");
        text.AppendLine("Untrusted customer/technician description (pre-repair only):");
        text.Append(Description.Trim());
        return text.ToString();
    }

    public RepairIntake Copy() => new()
    {
        Manufacturer = Manufacturer, Model = Model, ModelYear = ModelYear, MileageKm = MileageKm,
        RepairCode = RepairCode, Description = Description, BaselineLaborHours = BaselineLaborHours,
        VisibleCorrosion = VisibleCorrosion, VisiblePriorDamage = VisiblePriorDamage, AsOfDate = AsOfDate
    };
}

public static class RepairCatalog
{
    public static readonly string[] Codes =
    [
        "CONTROL_ARM_FRONT_LEFT_REPLACE", "CONTROL_ARM_FRONT_PAIR_REPLACE",
        "CONTROL_ARM_FRONT_RIGHT_REPLACE", "CONTROL_ARM_REAR_LEFT_REPLACE",
        "CONTROL_ARM_REAR_RIGHT_REPLACE"
    ];

    public static string Label(string code) => code.Replace('_', ' ').ToLowerInvariant();
}

public static partial class IntakeConflictDetector
{
    public static IReadOnlyList<string> Find(RepairIntake intake)
    {
        var conflicts = new List<string>();
        var text = intake.Description;
        if (intake.ModelYear is not null)
        {
            var years = YearRegex().Matches(text).Select(match => int.Parse(match.Value)).Distinct();
            if (years.Any(year => year != intake.ModelYear))
                conflicts.Add("The model year in the description conflicts with the structured model year.");
        }
        if (intake.MileageKm is not null)
        {
            var match = MileageRegex().Match(text);
            if (match.Success && int.TryParse(match.Groups[1].Value.Replace(".", "").Replace(",", ""), out var value)
                && Math.Abs(value - intake.MileageKm.Value) > 100)
                conflicts.Add("The mileage in the description conflicts with the structured mileage.");
        }
        return conflicts;
    }

    [GeneratedRegex(@"\b(?:19|20)\d{2}\b")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"\b([0-9][0-9.,]{2,8})\s*(?:km|kilometers?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MileageRegex();
}
