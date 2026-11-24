# Runs the browser (end-to-end) tests.
#
# What it does:
#   1. Starts the API on temporary databases (names start with "ele2e_"), so your real data is not touched.
#   2. Starts the website.
#   3. Runs the Playwright tests in a real browser.
#   4. Stops everything and deletes the temporary databases.
#
# How to run (from the repository root):   powershell -File scripts\run-e2e.ps1
# Needs: local PostgreSQL (settings are read from E-Learning\appsettings.Development.json).

# With -ServersOnly the script only starts the API and the website on temporary databases and waits.
# Then run the tests yourself from another terminal, in the "web" folder, in this order:
#   npx playwright test e2e/01-accounts-and-login.spec.ts
#   npx playwright test e2e/02-teacher-management.spec.ts
# Press Ctrl+C here when you finish: the servers stop and the temporary databases are deleted.
param([switch]$ServersOnly)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$apiPort = 5251
$webPort = 5183

# ----- Read the PostgreSQL server settings the developer already uses -----
$devSettings = Get-Content (Join-Path $root "E-Learning\appsettings.Development.json") -Raw | ConvertFrom-Json
$serverConnectionString = $devSettings.Tenancy.ServerConnectionString
$prefix = "ele2e_" + ([guid]::NewGuid().ToString("N").Substring(0, 8)) + "_"

# ----- Settings for the temporary API (environment variables override the settings files) -----
$env:ASPNETCORE_ENVIRONMENT = "E2E"
$env:ConnectionStrings__HubConnection = "$serverConnectionString;Database=${prefix}hub;Pooling=false"
$env:Tenancy__ServerConnectionString = "$serverConnectionString;Pooling=false"
$env:Tenancy__DatabasePrefix = "${prefix}t_"
$env:JWTOptions__SecretKey = "e2e-test-signing-key-long-enough-0123456789abcdef"
$env:JWTOptions__Issuer = "elearning-e2e"
$env:JWTOptions__Audience = "elearning-e2e"
$env:SeedAdmin__PhoneNumber = "01000000009"
$env:SeedAdmin__Password = "Admin12345"
$env:SeedAdmin__FullName = "E2E Admin"
$env:RateLimiting__AuthCallsPerMinute = "1000"

$apiProcess = $null
$webProcess = $null
$exitCode = 1

function Wait-ForUrl($url, $seconds) {
    for ($i = 0; $i -lt $seconds; $i++) {
        try {
            Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 2 | Out-Null
            return $true
        } catch {
            # Any HTTP answer (even 401 or 404) means the server is up.
            if ($_.Exception.Response) { return $true }
            Start-Sleep -Seconds 1
        }
    }
    return $false
}

function Stop-Tree($process) {
    if ($process -and -not $process.HasExited) {
        # /T also stops child processes (dotnet run and npm both start children).
        & taskkill /PID $process.Id /T /F | Out-Null
    }
}

try {
    Write-Host "Building the API..."
    & dotnet build (Join-Path $root "E-Learning\E-Learning.API.csproj") -nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "The API did not build." }

    Write-Host "Starting the API on port $apiPort (databases: $prefix*)..."
    $apiProcess = Start-Process dotnet -PassThru -WindowStyle Hidden `
        -ArgumentList "run", "--project", "`"$root\E-Learning\E-Learning.API.csproj`"", "--no-build", "--no-launch-profile", "--urls", "http://127.0.0.1:$apiPort" `
        -RedirectStandardOutput (Join-Path $env:TEMP "elearning-e2e-api.log")
    if (-not (Wait-ForUrl "http://127.0.0.1:$apiPort/api/auth/tenants/none" 60)) { throw "The API did not start. See $env:TEMP\elearning-e2e-api.log" }

    Write-Host "Starting the website on port $webPort..."
    $env:API_URL = "http://127.0.0.1:$apiPort"
    $webProcess = Start-Process cmd -PassThru -WindowStyle Hidden `
        -ArgumentList "/c", "npx vite --host 127.0.0.1 --port $webPort --strictPort" `
        -WorkingDirectory (Join-Path $root "web") `
        -RedirectStandardOutput (Join-Path $env:TEMP "elearning-e2e-web.log")
    if (-not (Wait-ForUrl "http://127.0.0.1:$webPort/" 60)) { throw "The website did not start. See $env:TEMP\elearning-e2e-web.log" }

    # Start every run with no values left over from the previous run.
    Remove-Item (Join-Path $root "web\e2e\.e2e-state.json") -ErrorAction SilentlyContinue

    Write-Host "Running the browser tests..."
    $env:E2E_BASE_URL = "http://127.0.0.1:$webPort"
    Push-Location (Join-Path $root "web")
    try {
        # The tests tell one story and must run in this order, on the same databases.
        # They are run as several short browser sessions instead of one long one:
        # on the development machine a browser session longer than about a minute was frozen
        # and closed from outside, so each step is kept short. "Grep" selects tests by their names.
        if ($ServersOnly) {
            Write-Host "Servers are ready. Website: http://127.0.0.1:$webPort   API: http://127.0.0.1:$apiPort"
            Write-Host "Run the tests from another terminal, then press Ctrl+C here."
            Start-Sleep -Seconds 3600
            $exitCode = 0
            return
        }

        $steps = @(
            @{ File = "01-accounts-and-login.spec.ts"; Grep = "opens in Arabic|invalid phone|wrong password|protected pages|sets up two-step|same link name|activates the account" },
            @{ File = "01-accounts-and-login.spec.ts"; Grep = "turns a teacher off" },
            @{ File = "01-accounts-and-login.spec.ts"; Grep = "two platforms|phone layout" },
            @{ File = "02-teacher-management.spec.ts"; Grep = "sets up a center|finds, suspends" },
            @{ File = "02-teacher-management.spec.ts"; Grep = "adds an assistant|does daily work|reviews what the assistant|fits a phone" },
            @{ File = "03-subscription-codes.spec.ts"; Grep = "owner generates|new student" },
            @{ File = "03-subscription-codes.spec.ts"; Grep = "owner sees|teacher sees" }
        )
        $exitCode = 0
        foreach ($step in $steps) {
            & cmd /c "npx playwright test e2e/$($step.File) --grep ""$($step.Grep)"""
            if ($LASTEXITCODE -ne 0) {
                # Later steps depend on the data of earlier ones, so stop at the first failure.
                $exitCode = $LASTEXITCODE
                break
            }
        }
    } finally {
        Pop-Location
    }
}
finally {
    Write-Host "Stopping the servers..."
    Stop-Tree $webProcess
    Stop-Tree $apiProcess

    Write-Host "Deleting the temporary databases..."
    $psql = Get-ChildItem "C:\Program Files\PostgreSQL\*\bin\psql.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($psql) {
        # Pull host, user and password out of the connection string for psql.
        $parts = @{}
        foreach ($pair in $serverConnectionString.Split(";")) {
            $keyValue = $pair.Split("=", 2)
            if ($keyValue.Length -eq 2) { $parts[$keyValue[0].Trim()] = $keyValue[1].Trim() }
        }
        $env:PGPASSWORD = $parts["Password"]
        $names = & $psql.FullName -h $parts["Host"] -U $parts["Username"] -d postgres -At -c "select datname from pg_database where datname like '$prefix%'"
        foreach ($name in $names) {
            & $psql.FullName -h $parts["Host"] -U $parts["Username"] -d postgres -q -c "DROP DATABASE IF EXISTS `"$name`" WITH (FORCE)" | Out-Null
        }
        $env:PGPASSWORD = $null
    } else {
        Write-Host "psql was not found. Delete databases starting with '$prefix' by hand."
    }
}

exit $exitCode
