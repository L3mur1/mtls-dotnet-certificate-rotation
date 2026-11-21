# mtls-dotnet-certificate-rotation

Server-side mTLS example in .NET: accepting two client certificates
during rotation, plus an xUnit project that calls the API over real HTTPS.

Article (PL): [mTLS w .NET – Jak świadomie potwierdzać tożsamość klienta w API](LINK)

## What this shows

- HTTPS with the ASP.NET / Visual Studio development certificate
- Requiring a client certificate in Kestrel
- Trusting clients via public `client-*.crt` files (fingerprint + CN + expiry from the cert)
- Accepting two certificates of the same client at once (rotation / grace)
- Rejecting certificates that are missing from that set or expired
- A sample business endpoint (`GET /invoices`) protected by mTLS
- An mTLS maintenance endpoint (`GET /mtls/check`) with fingerprint and expiry
- Live HTTP tests (`MtlsRotationClient`) using current and previous client certificates (Verify snapshots)

## What this does not show

- Issuing certificates for clients (CSR, signing). That is described in the article only.
- Authorization: a certificate identifies the client, it does not carry permissions.
- In-process `WebApplicationFactory` mTLS (TestServer has no real TLS client-certificate handshake).

## Requirements

- .NET SDK 11
- OpenSSL on PATH
- Windows + PowerShell
- Trusted ASP.NET HTTPS dev cert: `dotnet dev-certs https --trust`

## Run locally

1. Generate client certificates (writes into both projects' `Resources/Certificates/`):

   ```powershell
   .\scripts\generate-certs.ps1
   ```

2. Start the API (terminal 1):

   ```powershell
   .\scripts\Start-Api.ps1
   ```

3. Run the HTTP tests (terminal 2):

   ```powershell
   .\scripts\Start-Client.ps1
   ```

   Or: `dotnet test --project src/MtlsRotationClient/MtlsRotationClient.csproj`

   First-time / refreshed snapshots: `$env:VERIFY_AUTO=1` (requires `global.json` MTP runner).

## Certificates

| Location | Files |
|---|---|
| (machine) | ASP.NET development certificate — API HTTPS |
| `MtlsRotationServer/Resources/Certificates/` | `ca.crt`, `client-current.crt`, `client-previous.crt` |
| `MtlsRotationClient/Resources/Certificates/` | `client-current.pfx`, `client-previous.pfx` |

Copied to build output (`PreserveNewest`) as `Resources/Certificates/`. Apps load them from `AppContext.BaseDirectory/Resources/Certificates`.

Server-side `.crt` files are **public** certificates (public key + metadata only — no private key, no password):
- `ca.crt` — trust anchor; the client certificate chain must be trusted via this CA
- `client-*.crt` — allowlist of accepted clients (thumbprint match; expired presented certs are rejected with a warning log)

Client-side `.pfx` files include the **private** key. Password: `MtlsRotationClient/appsettings.json` → `ClientCertificate:Password`.

Both client certs use CN `acme-payments` (same `ClientId`).

## Project structure

- [`src/MtlsRotation.slnx`](src/MtlsRotation.slnx) — solution
- [`src/MtlsRotationServer/`](src/MtlsRotationServer/) — API (Kestrel mTLS)
- [`src/MtlsRotationClient/`](src/MtlsRotationClient/) — xUnit HTTP tests (live API)
- [`scripts/generate-certs.ps1`](scripts/generate-certs.ps1) — generates and copies client certs into both `Resources/Certificates/` folders
- [`scripts/Start-Api.ps1`](scripts/Start-Api.ps1) / [`scripts/Start-Client.ps1`](scripts/Start-Client.ps1)

## License

MIT
