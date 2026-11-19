#Requires -Version 5.1
param(
    [ValidateSet("current", "previous")]
    [string]$Cert = "current",
    [string]$BaseUrl = "https://localhost:5001"
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RootDir "src\MtlsRotationClient\MtlsRotationClient.csproj"
$ClientPfx = Join-Path $RootDir "src\MtlsRotationClient\Resources\client-current.pfx"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET SDK is required and must be on PATH."
}

if (-not (Test-Path $ClientPfx)) {
    Write-Host "Client certificates missing. Running .\scripts\generate-certs.ps1 ..."
    & (Join-Path $PSScriptRoot "generate-certs.ps1")
}

Write-Host "Calling API with client cert '$Cert' ..."
Set-Location $RootDir
dotnet run --project $Project -- $Cert $BaseUrl
