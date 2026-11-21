#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$ServerDir = Join-Path $RootDir "src\MtlsRotationServer"
$ClientDir = Join-Path $RootDir "src\MtlsRotationClient"
$ServerResources = Join-Path $ServerDir "Resources\Certificates"
$ClientResources = Join-Path $ClientDir "Resources\Certificates"
$WorkDir = Join-Path $RootDir "artifacts\cert-build"
$Password = "changeit"
$DaysCurrent = 825
$DaysPrevious = 400

if (-not (Get-Command openssl -ErrorAction SilentlyContinue)) {
    Write-Error "OpenSSL is required and must be on PATH."
}

function Reset-Directory {
    param([string]$Path)
    if (Test-Path $Path) {
        Remove-Item -Recurse -Force $Path
    }
    New-Item -ItemType Directory -Path $Path | Out-Null
}

Reset-Directory $WorkDir
Reset-Directory $ServerResources
Reset-Directory $ClientResources
Push-Location $WorkDir

try {
    Write-Host "Generating demo CA (for client certificates only)..."
    openssl genrsa -out ca.key 2048
    openssl req -x509 -new -nodes -key ca.key -sha256 -days $DaysCurrent `
        -subj "/CN=mTLS Demo CA" -out ca.crt

    function New-ClientCert {
        param(
            [string]$Name,
            [int]$Days,
            [string]$CommonName
        )

        openssl genrsa -out "$Name.key" 2048
        openssl req -new -key "$Name.key" -subj "/CN=$CommonName" -out "$Name.csr"
        openssl x509 -req -in "$Name.csr" -CA ca.crt -CAkey ca.key -CAcreateserial `
            -out "$Name.crt" -days $Days -sha256
        openssl pkcs12 -export -out "$Name.pfx" -inkey "$Name.key" -in "$Name.crt" `
            -passout "pass:$Password"
    }

    Write-Host "Generating client certificates..."
    # Same CN = same ClientId; two public certs on the server = rotation / grace.
    # HTTPS for the API uses the ASP.NET / Visual Studio development certificate.
    New-ClientCert -Name "client-current" -Days $DaysCurrent -CommonName "acme-payments"
    New-ClientCert -Name "client-previous" -Days $DaysPrevious -CommonName "acme-payments"

    function Get-Fingerprint {
        param([string]$CertPath)
        $line = openssl x509 -in $CertPath -noout -fingerprint -sha256
        ($line -split "=", 2)[1].Replace(":", "").ToUpperInvariant()
    }

    $fpCurrent = Get-Fingerprint client-current.crt
    $fpPrevious = Get-Fingerprint client-previous.crt

    Write-Host "Copying server Resources..."
    Copy-Item ca.crt, client-current.crt, client-previous.crt -Destination $ServerResources

    Write-Host "Copying client Resources..."
    Copy-Item client-current.pfx, client-previous.pfx -Destination $ClientResources

    $summary = @"
HTTPS           : ASP.NET development certificate (dotnet dev-certs https --trust)
server Resources/Certificates: ca.crt, client-current.crt, client-previous.crt
client Resources/Certificates: client-current.pfx, client-previous.pfx
client-current  : $fpCurrent  (CN=acme-payments)
client-previous : $fpPrevious  (CN=acme-payments, rotation / grace)
PFX password    : $Password  (MtlsRotationClient/appsettings.json)
"@
    Set-Content -Path (Join-Path $WorkDir "fingerprints.txt") -Value $summary -Encoding utf8

    Write-Host ""
    Write-Host $summary
}
finally {
    Pop-Location
}
