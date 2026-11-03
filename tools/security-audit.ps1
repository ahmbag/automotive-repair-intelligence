[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (git rev-parse --show-toplevel).Trim()
if (-not $repositoryRoot) {
    throw 'Run this script from inside the ARI Git repository.'
}

Push-Location $repositoryRoot
try {
    $failures = [System.Collections.Generic.List[string]]::new()
    # Include already tracked files and untracked, non-ignored files that would enter the next commit.
    $publicationFiles = @(git ls-files --cached --others --exclude-standard)

    $forbiddenPaths = @(
        '(^|/)(bin|obj|\.venv|\.pytest_cache|TestResults|playwright-report|blob-report|logs?)(/|$)',
        '\.(dll|exe|pdb|user|suo|log)$',
        '(^|/)\.env($|\.)',
        '(^|/)(secrets\.json|appsettings\.Local\.json)$'
    )
    foreach ($path in $publicationFiles) {
        foreach ($pattern in $forbiddenPaths) {
            if ($path -match $pattern) {
                $failures.Add("Forbidden tracked path: $path")
                break
            }
        }
    }

    $secretPatterns = [ordered]@{
        'OpenAI-style API key' = 'sk-(proj-)?[A-Za-z0-9_-]{20,}'
        'private key block' = '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----'
        'long bearer credential' = '(Bearer|Authorization:)[[:space:]]+[A-Za-z0-9._~-]{20,}'
    }
    $binaryExtensions = @('.png', '.jpg', '.jpeg', '.gif', '.ico', '.zip', '.dll', '.exe', '.pdb', '.joblib')
    foreach ($path in $publicationFiles) {
        if ($binaryExtensions -contains [System.IO.Path]::GetExtension($path).ToLowerInvariant()) {
            continue
        }
        $content = Get-Content -Raw -LiteralPath $path -ErrorAction Stop
        foreach ($entry in $secretPatterns.GetEnumerator()) {
            $dotNetPattern = $entry.Value -replace '\[\[:space:\]\]', '\s'
            if ($content -match $dotNetPattern) {
                $failures.Add("$($entry.Key) candidate in current file: $path (value suppressed)")
            }
        }
    }

    $commits = @(git rev-list --all)
    foreach ($commit in $commits) {
        foreach ($entry in $secretPatterns.GetEnumerator()) {
            $matches = @(git grep -n -I -E -e $entry.Value $commit -- ':!*.png' 2>$null)
            if ($LASTEXITCODE -eq 0) {
                foreach ($match in $matches) {
                    $location = ($match -split ':', 4)[0..2] -join ':'
                    $failures.Add("$($entry.Key) candidate at $location (value suppressed)")
                }
            }
            elseif ($LASTEXITCODE -gt 1) {
                throw "git grep failed while scanning $commit for $($entry.Key)."
            }
        }
    }

    $configurationFiles = @($publicationFiles | Where-Object { $_ -match '(^|/)appsettings[^/]*\.json$' })
    foreach ($path in $configurationFiles) {
        $json = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json -AsHashtable
        $stack = [System.Collections.Generic.Stack[object]]::new()
        $stack.Push(@('', $json))
        while ($stack.Count -gt 0) {
            $item = $stack.Pop()
            $prefix = [string]$item[0]
            $node = $item[1]
            if ($node -is [System.Collections.IDictionary]) {
                foreach ($key in $node.Keys) {
                    $name = if ($prefix) { "$prefix`:$key" } else { [string]$key }
                    $value = $node[$key]
                    if ($value -is [System.Collections.IDictionary]) {
                        $stack.Push(@($name, $value))
                    }
                    elseif ($key -match '(?i)(password|apikey|connectionstring|clientsecret|token)$' -and
                            $null -ne $value -and -not [string]::IsNullOrWhiteSpace([string]$value)) {
                        $failures.Add("Non-empty sensitive configuration value: $path -> $name")
                    }
                }
            }
        }
    }

    if ($failures.Count -gt 0) {
        $failures | Sort-Object -Unique | ForEach-Object { Write-Error $_ }
        throw "Security audit failed with $($failures.Count) finding(s)."
    }

    Write-Host "Security audit passed: $($publicationFiles.Count) publication files and $($commits.Count) commit(s) scanned."
    Write-Host 'No known API-key/private-key/bearer pattern, committed secret config, or forbidden local artifact was found.'
}
finally {
    Pop-Location
}
