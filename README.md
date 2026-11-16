# mtls-dotnet-certificate-rotation

Server-side mTLS example in .NET: accepting two client certificates
during rotation, with expiry warnings and a health check.

Article (PL): [mTLS w .NET – Jak świadomie potwierdzać tożsamość klienta w API](LINK)

## What this shows

- Requiring a client certificate in Kestrel
- A certificate registry (client, SHA-256 fingerprint, status, expiry)
- Accepting two certificates of the same client at once (active + grace period)
- Rejecting revoked and expired certificates
- Logging a warning when a certificate expires soon
- A health check endpoint that reports which certificate the server saw

## What this does not show

- Issuing certificates for clients (CSR, signing). That is described in the article only.
- Authorization: a certificate identifies the client, it does not carry permissions.

## Requirements

- .NET SDK (version)
- OpenSSL (to generate test certificates)

## Running locally

1. Generate a test CA and client certificates: `./scripts/generate-certs.sh`
2. Start the API: `dotnet run`
3. Call it with a client certificate:
   `curl --cert certs/client-new.crt --key certs/client-new.key https://localhost:5001/mtls/check`

## Scenarios to try

| Certificate | Expected result |
|---|---|
| active | accepted |
| in grace period | accepted |
| revoked | rejected |
| expiring soon | accepted, warning in logs |
| unknown | connection refused |

## Project structure

Short list of key files (registry, validation, health check).

## License

MIT
