using ARI.Web.Components;
using ARI.Web.Clients;
using ARI.Web.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddOptions<AriApiOptions>()
    .Bind(builder.Configuration.GetSection(AriApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<LiveAriApiClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<AriApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddScoped<FixtureAriApiClient>();
builder.Services.AddScoped<IAriApiClient>(services =>
    services.GetRequiredService<IOptions<AriApiOptions>>().Value.UseFixture
        ? services.GetRequiredService<FixtureAriApiClient>()
        : services.GetRequiredService<LiveAriApiClient>());
builder.Services.AddScoped<RepairWorkspace>();
builder.Services.AddScoped<RepairAnalysisCoordinator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/health-ui", () => Results.Ok(new { status = "Healthy", component = "ARI.Web" }));

app.Run();
