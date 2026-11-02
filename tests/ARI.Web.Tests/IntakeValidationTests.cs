using System.ComponentModel.DataAnnotations;
using ARI.Web.Models;

namespace ARI.Web.Tests;

public sealed class IntakeValidationTests
{
    [Fact]
    public void NegativeMileage_IsRejectedBeforeApiCall()
    {
        var intake = DemoCases.Base();
        intake.MileageKm = -1;
        var results = Validate(intake);
        Assert.Contains(results, item => item.MemberNames.Contains(nameof(RepairIntake.MileageKm)));
    }

    [Fact]
    public void ConflictingModelYear_IsNeverSilentlyMerged()
    {
        var intake = DemoCases.Base();
        intake.ModelYear = 2019;
        var results = Validate(intake);
        Assert.Contains(results, item => item.ErrorMessage!.Contains("conflicts", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalysisDocument_LabelsStructuredAndUntrustedContent()
    {
        var document = DemoCases.Base().BuildAnalysisDocument();
        Assert.Contains("Trusted structured pre-repair intake:", document);
        Assert.Contains("Untrusted customer/technician description", document);
        Assert.DoesNotContain("Actual labor:", document);
    }

    private static IReadOnlyList<ValidationResult> Validate(RepairIntake model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
