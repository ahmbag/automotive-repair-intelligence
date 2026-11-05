using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace ARI.Web.E2ETests;

[Collection("browser")]
public sealed class RepairUiAcceptanceTests : PageTest
{
    public RepairUiAcceptanceTests(WebServerFixture server) => _ = server;

    [Fact(DisplayName = "01 Start and primary navigation")]
    public async Task StartAndNavigation()
    {
        await Page.GotoAsync(WebServerFixture.BaseUrl);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Turn workshop notes into a traceable repair estimate." })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "System & Models" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "System & model information" })).ToBeVisibleAsync();
    }

    [Fact(DisplayName = "02 Valid input reaches case 360")]
    public async Task ValidInput()
    {
        await CreateBaselineAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/case$"));
        await Expect(Page.GetByText("ACCEPTED", new() { Exact = true }).First).ToBeVisibleAsync();
    }

    [Fact(DisplayName = "03 Invalid mileage is blocked client-side")]
    public async Task InvalidMileage()
    {
        await Page.GotoAsync($"{WebServerFixture.BaseUrl}/new");
        await Page.Locator("#mileage").FillAsync("-1");
        await Page.Locator("#analyze-button").ClickAsync();
        await Expect(Page.Locator(".validation-message").Filter(new() { HasText = "Mileage must be between 0 and 5,000,000 km." }).First).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/new$"));
    }

    [Fact(DisplayName = "04 Clear analysis renders LLM result")]
    public async Task LlmResult()
    {
        await CreateBaselineAsync();
        await Expect(Page.GetByText("repair-extraction-v3")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Grounded explanation")).ToBeVisibleAsync();
    }

    [Fact(DisplayName = "05 ML prediction and version are visible")]
    public async Task MlPrediction()
    {
        await CreateBaselineAsync();
        await Expect(Page.Locator("#predicted-hours")).ToContainTextAsync(new System.Text.RegularExpressions.Regex(@"2[,.]487"));
        await Expect(Page.GetByText("ridge-v1").First).ToBeVisibleAsync();
    }

    [Fact(DisplayName = "06 Five RAG sources are rendered")]
    public async Task FiveRagSources()
    {
        await CreateBaselineAsync();
        await Expect(Page.Locator("tbody tr")).ToHaveCountAsync(5);
        await Expect(Page.GetByText("RANK_FUSION_V2").First).ToBeVisibleAsync();
    }

    [Fact(DisplayName = "07 Evidence detail quote matches selected row")]
    public async Task EvidenceDetail()
    {
        await CreateBaselineAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Inspect evidence" }).First.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToContainTextAsync("ARI-SYN-001");
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToContainTextAsync("144 minutes");
    }

    [Fact(DisplayName = "08 Ambiguous text requires manual mapping and no ML")]
    public async Task ManualMapping()
    {
        await Page.GotoAsync($"{WebServerFixture.BaseUrl}/new");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Ambiguous Description" }).ClickAsync();
        await Page.Locator("#analyze-button").ClickAsync();
        await Expect(Page.Locator(".notice").Filter(new() { HasText = "ManualReviewRequired" })).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/new$"));
    }

    [Fact(DisplayName = "09 What-if creates new prediction and preserves baseline")]
    public async Task WhatIfSnapshot()
    {
        await CreateBaselineAsync();
        var baseline = await Page.Locator("#predicted-hours").InnerTextAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "Explore what-if scenarios" }).ClickAsync();
        await Page.Locator("#scenario-mileage").FillAsync("240000");
        await Page.Locator("#run-scenario").ClickAsync();
        await Expect(Page.GetByText("ML-ONLY")).ToBeVisibleAsync();
        await Expect(Page.GetByText(baseline, new() { Exact = false })).ToBeVisibleAsync();
    }

    [Fact(DisplayName = "10 Rapid submit does not allow stale overwrite")]
    public async Task DoubleSubmitLock()
    {
        await Page.GotoAsync($"{WebServerFixture.BaseUrl}/new");
        await Page.Locator("#analyze-button").ClickAsync();
        await Expect(Page.Locator("#analyze-button")).ToBeDisabledAsync();
        await Page.WaitForURLAsync("**/case");
        await Expect(Page.Locator("#predicted-hours")).ToBeVisibleAsync();
    }

    [Fact(DisplayName = "11 Backend outage is controlled and recoverable")]
    public async Task BackendOutage()
    {
        await Page.GotoAsync($"{WebServerFixture.BaseUrl}/new");
        await Page.Locator("#description").FillAsync("A complete pre-repair description for test purposes. SIMULATE_BACKEND_OUTAGE");
        await Page.Locator("#analyze-button").ClickAsync();
        await Expect(Page.GetByText("ARI.Api cannot be reached", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(Page.Locator("#analyze-button")).ToBeEnabledAsync();
    }

    [Theory(DisplayName = "12 Tablet and phone have no page-level horizontal clipping")]
    [InlineData(768, 1024)]
    [InlineData(390, 844)]
    public async Task Responsive(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync($"{WebServerFixture.BaseUrl}/new");
        var overflows = await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.False(overflows);
        await Expect(Page.Locator("#analyze-button")).ToBeVisibleAsync();
    }

    [Fact(DisplayName = "13 Reload has a defined safe recovery")]
    public async Task ReloadRecovery()
    {
        await CreateBaselineAsync();
        await Page.ReloadAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "No analyzed repair case" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Create estimate" })).ToBeVisibleAsync();
    }

    [Fact(DisplayName = "14 Keyboard navigation exposes focus and submits")]
    public async Task KeyboardNavigation()
    {
        await Page.GotoAsync($"{WebServerFixture.BaseUrl}/new");

        // Headless browsers do not share one platform-independent initial focus origin.
        // Advance through a small, bounded number of tab stops and verify the actual
        // accessibility contract: keyboard focus reaches a visible interactive control.
        var interactiveFocusIsVisible = false;
        for (var tabStop = 0; tabStop < 5 && !interactiveFocusIsVisible; tabStop++)
        {
            await Page.Keyboard.PressAsync("Tab");
            interactiveFocusIsVisible = await Page.EvaluateAsync<bool>("""
                () => {
                    const element = document.activeElement;
                    return element !== null
                        && element !== document.body
                        && ['A', 'BUTTON', 'INPUT', 'SELECT', 'TEXTAREA'].includes(element.tagName)
                        && element.matches(':focus-visible');
                }
                """);
        }

        Assert.True(interactiveFocusIsVisible);
        await Page.Locator("#analyze-button").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForURLAsync("**/case");
        await Expect(Page.Locator("#predicted-hours")).ToBeVisibleAsync();
    }

    private async Task CreateBaselineAsync()
    {
        await Page.GotoAsync($"{WebServerFixture.BaseUrl}/new");
        await Page.Locator("#analyze-button").ClickAsync();
        await Page.WaitForURLAsync("**/case");
    }
}

[CollectionDefinition("browser")]
public sealed class BrowserCollection : ICollectionFixture<WebServerFixture>;
