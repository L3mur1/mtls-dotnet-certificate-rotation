# mtls-dotnet-certificate-rotation

Server-side mTLS example in .NET: accepting two client certificates
during rotation, plus an xUnit project that calls the API over real HTTPS.

Article (PL): [mTLS w .NET – Jak świadomie potwierdzać tożsamość klienta w API](LINK)

## What this shows

- HTTPS with the ASP.NET / Visual Studio development certificate
- Requiring a client certificate in Kestrel
- Trusting any number of public client `.crt` files + any number of CA `.crt` files (thumbprint allowlist + CustomRootTrust)
- Demo ships two client certs and two CAs (client + CA rotation / grace); expired files skipped at startup with a warning
- Rejecting certificates missing from the allowlist or failing chain (including NotTimeValid), with warning logs
- MVC controllers for `GET /invoices` and `GET /mtls/check` (cert claims on `User`)
- Live HTTP tests (`MtlsRotationClient`): trusted current/previous, reject without cert, reject untrusted cert / unknown CA

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

1. Generate certificates (writes into `Resources/CACerts` + `Resources/ClientCerts`):

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

## Certificates

| Location | Files |
|---|---|
| (machine) | ASP.NET development certificate — API HTTPS |
| `MtlsRotationServer/Resources/CACerts/` | any `*.crt` trust anchors (demo: `ca-current.crt`, `ca-previous.crt`) |
| `MtlsRotationServer/Resources/ClientCerts/` | any `*.crt` allowlist entries (demo: `acme-payments-current.crt`, `acme-payments-previous.crt`) |
| `MtlsRotationClient/Resources/ClientCerts/` | client `*.pfx` (demo: `acme-payments-current.pfx`, `acme-payments-previous.pfx`, `acme-payments-unknown-ca.pfx`) |

Copied to build output (`PreserveNewest`). Apps load them from `AppContext.BaseDirectory/Resources/...`.

Naming: **ClientCerts** (not “request certs”) — these are client identity certificates used in mTLS. “Request” usually means a CSR.

Server-side `.crt` files are **public** (public key + metadata only — no private key, no password):
- `CACerts/*.crt` — trust anchors (`CustomRootTrust`); chain may end at **any** configured CA. Any count (demo uses two for CA rotation). Project choice.
- `ClientCerts/*.crt` — allowlist by thumbprint. Any count (demo uses two for client cert rotation).

At startup, **expired** files in either folder are **skipped** with a warning. If none remain usable, the API fails fast.

In this demo `acme-payments-current` is signed by `ca-current`, `acme-payments-previous` by `ca-previous`.
`acme-payments-unknown-ca.pfx` uses the same CN but an unknown CA — present only on the client for a negative test (not copied to the server).

Client-side `.pfx` files include the **private** key. Password: `MtlsRotationClient/appsettings.json` → `ClientCertificate:Password`.

Trusted client certs use CN `acme-payments` (same `ClientId`); file names encode the rotation slot.

## Project structure

- [`src/MtlsRotation.slnx`](src/MtlsRotation.slnx) — solution
- [`src/MtlsRotationServer/`](src/MtlsRotationServer/) — API (Kestrel mTLS)
- [`src/MtlsRotationClient/`](src/MtlsRotationClient/) — xUnit HTTP tests (live API)
- [`scripts/generate-certs.ps1`](scripts/generate-certs.ps1) — generates CAs + client certs into `Resources/CACerts` and `Resources/ClientCerts`
- [`scripts/Start-Api.ps1`](scripts/Start-Api.ps1) / [`scripts/Start-Client.ps1`](scripts/Start-Client.ps1)

## License

MIT
