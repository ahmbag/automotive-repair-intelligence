using System.Diagnostics;

namespace ARI.Web.E2ETests;

public sealed class WebServerFixture : IAsyncLifetime
{
    public const string BaseUrl = "http://127.0.0.1:5279";
    private Process? process;

    public async Task InitializeAsync()
    {
        var root = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        var start = new ProcessStartInfo("dotnet",
            $"run --project \"{Path.Combine(root, "src", "ARI.Web", "ARI.Web.csproj")}\" " +
            $"--configuration {configuration} --no-build --no-launch-profile --urls {BaseUrl}")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.Environment["AriApi__UseFixture"] = "true";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        process = Process.Start(start) ?? throw new InvalidOperationException("Could not start ARI.Web.");
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                using var response = await client.GetAsync($"{BaseUrl}/health-ui");
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(250);
        }
        throw new TimeoutException("ARI.Web did not become ready for browser automation.");
    }

    public Task DisposeAsync()
    {
        if (process is { HasExited: false })
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        process?.Dispose();
        return Task.CompletedTask;
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
