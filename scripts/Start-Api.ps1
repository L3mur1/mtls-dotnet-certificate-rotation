#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RootDir "src\MtlsRotationServer\MtlsRotationServer.csproj"
$ClientCurrentCrt = Join-Path $RootDir "src\MtlsRotationServer\Resources\Certificates\client-current.crt"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET SDK is required and must be on PATH."
}

if (-not (Test-Path $ClientCurrentCrt)) {
    Write-Host "Client certificates missing. Running .\scripts\generate-certs.ps1 ..."
    & (Join-Path $PSScriptRoot "generate-certs.ps1")
}

Write-Host "Starting API on https://localhost:5001 ..."
Write-Host "Uses the ASP.NET development certificate. If needed: dotnet dev-certs https --trust"
Set-Location $RootDir
dotnet run --project $Project
