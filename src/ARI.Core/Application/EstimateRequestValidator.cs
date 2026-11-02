using ARI.Core.Contracts;

namespace ARI.Core.Application;

public static class EstimateRequestValidator
{
    public const string ReferenceRepairCode = "SUSPENSION_CONTROL_ARM_REPLACE";

    public static IReadOnlyCollection<string> Validate(
        CreateEstimateRequest? request,
        int currentYear)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Request is required.");
            return errors;
        }

        if (request.Vehicle is null)
        {
            errors.Add("Vehicle is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Vehicle.Manufacturer))
        {
            errors.Add("Vehicle manufacturer is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Vehicle.Model))
        {
            errors.Add("Vehicle model is required.");
        }

        if (request.Vehicle.ModelYear is < 1886 || request.Vehicle.ModelYear > currentYear + 1)
        {
            errors.Add("Vehicle model year is outside the supported range.");
        }

        if (request.Vehicle.MileageKm < 0)
        {
            errors.Add("Vehicle mileage must not be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.RepairCode))
        {
            errors.Add("Repair code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors.Add("Repair description is required.");
        }

        return errors;
    }
}
