#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$ServerDir = Join-Path $RootDir "src\MtlsRotationServer"
$ClientDir = Join-Path $RootDir "src\MtlsRotationClient"
$ServerCACerts = Join-Path $ServerDir "Resources\CACerts"
$ServerClientCerts = Join-Path $ServerDir "Resources\ClientCerts"
$ClientClientCerts = Join-Path $ClientDir "Resources\ClientCerts"
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
Reset-Directory $ServerCACerts
Reset-Directory $ServerClientCerts
Reset-Directory $ClientClientCerts

# Remove legacy flat Resources/Certificates if present.
@(
    (Join-Path $ServerDir "Resources\Certificates"),
    (Join-Path $ClientDir "Resources\Certificates")
) | ForEach-Object {
    if (Test-Path $_) {
        Remove-Item -Recurse -Force $_
    }
}

Push-Location $WorkDir

try {
    function New-Ca {
        param(
            [string]$Name,
            [int]$Days,
            [string]$CommonName
        )

        openssl genrsa -out "$Name.key" 2048
        openssl req -x509 -new -nodes -key "$Name.key" -sha256 -days $Days `
            -subj "/CN=$CommonName" -out "$Name.crt"
    }

    function New-ClientCert {
        param(
            [string]$Name,
            [int]$Days,
            [string]$CommonName,
            [string]$CaName
        )

        openssl genrsa -out "$Name.key" 2048
        openssl req -new -key "$Name.key" -subj "/CN=$CommonName" -out "$Name.csr"
        openssl x509 -req -in "$Name.csr" -CA "$CaName.crt" -CAkey "$CaName.key" -CAcreateserial `
            -out "$Name.crt" -days $Days -sha256
        openssl pkcs12 -export -out "$Name.pfx" -inkey "$Name.key" -in "$Name.crt" `
            -passout "pass:$Password"
    }

    Write-Host "Generating demo CAs (current + previous for CA rotation / grace)..."
    New-Ca -Name "ca-previous" -Days $DaysPrevious -CommonName "mTLS Demo CA (previous)"
    New-Ca -Name "ca-current" -Days $DaysCurrent -CommonName "mTLS Demo CA (current)"

    Write-Host "Generating client certificates (each signed by its CA)..."
    # Same CN = same ClientId; any number of public client certs on the server = rotation / grace.
    # HTTPS for the API uses the ASP.NET / Visual Studio development certificate.
    New-ClientCert -Name "client-previous" -Days $DaysPrevious -CommonName "acme-payments" -CaName "ca-previous"
    New-ClientCert -Name "client-current" -Days $DaysCurrent -CommonName "acme-payments" -CaName "ca-current"

    # Untrusted: same CN, but unknown CA — not copied to server CACerts / ClientCerts (negative test only).
    New-Ca -Name "ca-untrusted" -Days $DaysCurrent -CommonName "mTLS Demo CA (untrusted)"
    New-ClientCert -Name "client-untrusted" -Days $DaysCurrent -CommonName "acme-payments" -CaName "ca-untrusted"

    function Get-Fingerprint {
        param([string]$CertPath)
        $line = openssl x509 -in $CertPath -noout -fingerprint -sha256
        ($line -split "=", 2)[1].Replace(":", "").ToUpperInvariant()
    }

    $fpCurrent = Get-Fingerprint client-current.crt
    $fpPrevious = Get-Fingerprint client-previous.crt
    $fpUntrusted = Get-Fingerprint client-untrusted.crt
    $fpCaCurrent = Get-Fingerprint ca-current.crt
    $fpCaPrevious = Get-Fingerprint ca-previous.crt

    Write-Host "Copying server Resources..."
    Copy-Item ca-current.crt, ca-previous.crt -Destination $ServerCACerts
    Copy-Item client-current.crt, client-previous.crt -Destination $ServerClientCerts

    Write-Host "Copying client Resources..."
    Copy-Item client-current.pfx, client-previous.pfx, client-untrusted.pfx -Destination $ClientClientCerts

    $summary = @"
HTTPS           : ASP.NET development certificate (dotnet dev-certs https --trust)
server Resources/CACerts     : ca-current.crt, ca-previous.crt
server Resources/ClientCerts : client-current.crt, client-previous.crt
client Resources/ClientCerts : client-current.pfx, client-previous.pfx, client-untrusted.pfx
ca-current      : $fpCaCurrent  (signs client-current)
ca-previous     : $fpCaPrevious  (signs client-previous; CA rotation / grace)
client-current  : $fpCurrent  (CN=acme-payments, issued by ca-current)
client-previous : $fpPrevious  (CN=acme-payments, issued by ca-previous)
client-untrusted: $fpUntrusted  (CN=acme-payments, unknown CA — not trusted by server)
PFX password    : $Password  (MtlsRotationClient/appsettings.json)
"@
    Set-Content -Path (Join-Path $WorkDir "fingerprints.txt") -Value $summary -Encoding utf8

    Write-Host ""
    Write-Host $summary
}
finally {
    Pop-Location
}
