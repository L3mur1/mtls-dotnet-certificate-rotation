#Requires -Version 5.1
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

Write-Host "Running mTLS HTTP tests against a live API (start .\scripts\Start-Api.ps1 first) ..."
Set-Location $RootDir
dotnet test $Project --no-restore
if ($LASTEXITCODE -ne 0) {
    # restore+test if packages missing
    dotnet test $Project
}
