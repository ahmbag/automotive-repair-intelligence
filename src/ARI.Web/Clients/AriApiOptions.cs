using System.ComponentModel.DataAnnotations;

namespace ARI.Web.Clients;

public sealed class AriApiOptions
{
    public const string SectionName = "AriApi";

    [Required, Url]
    public string BaseUrl { get; init; } = "http://127.0.0.1:5012";

    [Range(5, 300)]
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>Only for deterministic browser automation; never presented as a live result.</summary>
    public bool UseFixture { get; init; }
}
