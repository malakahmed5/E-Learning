# Starts the project on your computer so you can try it in the browser.
#
# It opens two windows:
#   1. The API (the backend) on http://localhost:5241
#   2. The website (the frontend) on http://localhost:5173
# and then opens the website in your browser.
#
# How to run (from the repository root):   powershell -File scripts\run-dev.ps1
# To stop: close the two windows.
#
# Needs: PostgreSQL running, and E-Learning\appsettings.Development.json filled in
# (copy appsettings.Development.example.json if the file is missing).

$root = Split-Path -Parent $PSScriptRoot

# The first time, the website needs its packages.
if (-not (Test-Path (Join-Path $root "web\node_modules"))) {
    Write-Host "Installing the website packages (first time only)..."
    Push-Location (Join-Path $root "web")
    & cmd /c "npm install"
    Pop-Location
}

Write-Host "Starting the API..."
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$root'; dotnet run --project E-Learning --launch-profile http"

Write-Host "Starting the website..."
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$root\web'; npm run dev"

# Give both a moment to start, then open the browser.
Start-Sleep -Seconds 12
Start-Process "http://localhost:5173"

Write-Host ""
Write-Host "Website: http://localhost:5173"
Write-Host "API documentation (Swagger): http://localhost:5241/swagger"
Write-Host "First login: the platform admin phone and password are in E-Learning\appsettings.Development.json under SeedAdmin."
