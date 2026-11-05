[CmdletBinding()]
param(
    [string]$PythonExecutable,
    [switch]$SkipRestore,
    [switch]$SkipBrowserInstall
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    if ([string]::IsNullOrWhiteSpace($PythonExecutable)) {
        $venvPython = if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
            Join-Path $repositoryRoot ".venv/Scripts/python.exe"
        }
        else {
            Join-Path $repositoryRoot ".venv/bin/python"
        }

        $PythonExecutable = if (Test-Path -LiteralPath $venvPython) { $venvPython } else { "python" }
    }

    if (-not $SkipRestore) {
        & dotnet restore AutomotiveRepairIntelligence.slnx --locked-mode
        if ($LASTEXITCODE -ne 0) { throw ".NET locked restore failed." }
    }

    & dotnet build AutomotiveRepairIntelligence.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw ".NET build failed." }

    foreach ($project in @(
        "tests/ARI.Core.Tests/ARI.Core.Tests.csproj",
        "tests/ARI.Api.Tests/ARI.Api.Tests.csproj",
        "tests/ARI.Web.Tests/ARI.Web.Tests.csproj"
    )) {
        & dotnet test $project --configuration Release --no-build --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Tests failed for $project." }
    }

    # The three trained-artifact acceptance tests are part of the live gate because model.joblib is
    # intentionally not published. The public checkout gate still exercises all model-independent ML code.
    & $PythonExecutable -m pytest ml/tests -k "not TrainedArtifactAcceptanceTests"
    if ($LASTEXITCODE -ne 0) { throw "Python tests failed." }

    $playwright = Join-Path $repositoryRoot "tests/ARI.Web.E2ETests/bin/Release/net10.0/playwright.ps1"
    if (-not $SkipBrowserInstall) {
        & $playwright install chromium
        if ($LASTEXITCODE -ne 0) { throw "Playwright Chromium installation failed." }
    }

    & dotnet test tests/ARI.Web.E2ETests/ARI.Web.E2ETests.csproj `
        --configuration Release --no-build --no-restore `
        --filter "Category!=Live&Category!=LiveCapture"
    if ($LASTEXITCODE -ne 0) { throw "Deterministic browser tests failed." }

    & (Join-Path $PSScriptRoot "security-audit.ps1")
    if ($LASTEXITCODE -ne 0) { throw "Publication security audit failed." }

    Write-Host "ARI-RC1 deterministic release validation passed."
}
finally {
    Pop-Location
}
