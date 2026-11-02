using Microsoft.Playwright;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ARI.Web.E2ETests;

/// <summary>
/// Explicit live-browser acceptance. Set ARI_LIVE_UI_URL to run against ARI.Web wired to the
/// real ARI.Api, HANA, OpenAI and Python ML services. The deterministic suite remains offline-safe.
/// </summary>
public sealed class LiveUiAcceptanceTests
{
    [Fact(DisplayName = "LIVE Capture repair intake without invoking external analysis")]
    [Trait("Category", "LiveCapture")]
    public async Task LiveIntakeScreenshot_UsesRealApiClientWithoutFixtureData()
    {
        var baseUrl = Environment.GetEnvironmentVariable("ARI_LIVE_UI_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Console.WriteLine("LIVE CAPTURE SKIPPED: set ARI_LIVE_UI_URL to capture the real intake view.");
            return;
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        await page.GotoAsync(baseUrl);
        await Assertions.Expect(page.GetByText("AUTOMATED TEST DATA")).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Link, new() { Name = "Start new estimate" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Repair intake" })).ToBeVisibleAsync();

        var screenshots = Path.Combine(FindRepositoryRoot(), "docs", "ui-screenshots");
        Directory.CreateDirectory(screenshots);
        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(screenshots, "04-repair-intake-live.png"),
            FullPage = true
        });
    }

    [Fact(DisplayName = "LIVE Real API, HANA, OpenAI and Python end-to-end")]
    [Trait("Category", "Live")]
    public async Task RealStack_EndToEndWithTwoWhatIfScenarios()
    {
        var baseUrl = Environment.GetEnvironmentVariable("ARI_LIVE_UI_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Console.WriteLine("LIVE SKIPPED: set ARI_LIVE_UI_URL to execute the external-service acceptance.");
            return;
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        var screenshots = Path.Combine(FindRepositoryRoot(), "docs", "ui-screenshots");
        Directory.CreateDirectory(screenshots);
        await page.GotoAsync(baseUrl);
        await page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, "01-dashboard-live.png"), FullPage = true });

        await page.GetByRole(AriaRole.Link, new() { Name = "Start new estimate" }).ClickAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, "04-repair-intake-live.png"), FullPage = true });
        await page.Locator("#analyze-button").ClickAsync();
        var navigation = page.WaitForURLAsync("**/case", new() { Timeout = 180_000 });
        var controlledFailure = page.Locator(".notice.error").WaitForAsync(
            new() { State = WaitForSelectorState.Visible, Timeout = 180_000 });
        var first = await Task.WhenAny(navigation, controlledFailure);
        if (first == controlledFailure)
            Assert.Fail($"Live pipeline did not complete: {await page.Locator(".notice.error").InnerTextAsync()}");
        await navigation;
        await Assertions.Expect(page.GetByText("AUTOMATED TEST DATA")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByText("ACCEPTED", new() { Exact = true }).First).ToBeVisibleAsync(new() { Timeout = 30_000 });
        var predictionText = await page.Locator("#predicted-hours").InnerTextAsync();
        var numeric = Regex.Match(predictionText, @"\d+[,.]\d+").Value.Replace(',', '.');
        Assert.True(decimal.TryParse(numeric, NumberStyles.Number, CultureInfo.InvariantCulture, out var predictedHours));
        Assert.InRange(predictedHours, 2.337136m, 2.637136m);
        await Assertions.Expect(page.Locator("tbody tr")).ToHaveCountAsync(5);
        await Assertions.Expect(page.GetByText("RANK_FUSION_V2").First).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Inspect evidence" }).First.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToContainTextAsync("Documented excerpt");
        await page.GetByRole(AriaRole.Button, new() { Name = "Close evidence details" }).ClickAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, "02-repair-case-360-live.png"), FullPage = true });

        var baseline = predictionText;
        await page.GetByRole(AriaRole.Link, new() { Name = "Explore what-if scenarios" }).ClickAsync();
        await page.Locator("#scenario-name").FillAsync("High mileage");
        await page.Locator("#scenario-mileage").FillAsync("240000");
        await page.Locator("#run-scenario").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "High mileage" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await page.Locator("#scenario-name").FillAsync("Visible corrosion");
        await page.GetByLabel("Visible corrosion").CheckAsync();
        await page.Locator("#run-scenario").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Visible corrosion" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(page.GetByText(baseline, new() { Exact = false })).ToBeVisibleAsync();
        await Assertions.Expect(
            page.Locator(".scenario-card .tech-tag").Filter(new() { HasText = "ML-ONLY" }))
            .ToHaveCountAsync(2);
        await page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, "03-what-if-live.png"), FullPage = true });
        Console.WriteLine($"LIVE ACCEPTED: baseline={baseline}; evidence=5; scenarios=2");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AutomotiveRepairIntelligence.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
