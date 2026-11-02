namespace ARI.Web.Models;

public sealed record DemoCase(string Name, string Description, Func<RepairIntake> Create);

public static class DemoCases
{
    public static readonly IReadOnlyList<DemoCase> All =
    [
        new("Standard Repair", "Complete, internally consistent pre-repair input.", () => Base()),
        new("Corrosion Risk", "Adds visible pre-repair corrosion as an allowed model feature.", () =>
        {
            var item = Base(); item.VisibleCorrosion = true;
            item.Description += " Visible surface corrosion is present around the left front control-arm mounting area.";
            return item;
        }),
        new("High Mileage", "A valid high-mileage vehicle, still below the domain ceiling.", () =>
        {
            var item = Base(); item.MileageKm = 318000;
            item.Description = item.Description.Replace("128000", "318000"); return item;
        }),
        new("Ambiguous Description", "Intentionally lacks a uniquely mappable vehicle and repair operation.", () => new()
        {
            Manufacturer = null, Model = null, ModelYear = null, MileageKm = null,
            RepairCode = RepairCatalog.Codes[0], BaselineLaborHours = 2.2m,
            Description = "The customer reports a noise from somewhere underneath. Inspect and repair as necessary."
        }),
        new("Missing Information", "Shows client-side validation before any API request.", () => new()
        {
            Manufacturer = null, Model = null, ModelYear = null, MileageKm = null,
            RepairCode = RepairCatalog.Codes[0], Description = string.Empty
        })
    ];

    public static RepairIntake Base() => new()
    {
        Manufacturer = "Volkswagen", Model = "Golf", ModelYear = 2018, MileageKm = 128000,
        RepairCode = "CONTROL_ARM_FRONT_LEFT_REPLACE", BaselineLaborHours = 2.2m,
        Description = "2018 Volkswagen Golf with 128000 km. Before repair, the left front control arm has excessive play and must be replaced. No post-repair information is available.",
        AsOfDate = DateTime.UtcNow.Date
    };
}
