using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using MtlsRotationServer.Certificates;

var builder = WebApplication.CreateBuilder(args);

// Copied to output via PreserveNewest on Resources/*
// HTTPS uses the ASP.NET Core / Visual Studio development certificate.
var certsDir = Path.Combine(AppContext.BaseDirectory, "Resources");
var caPath = Path.Combine(certsDir, "ca.crt");

if (!File.Exists(caPath))
{
    throw new InvalidOperationException(
        "Demo CA is missing. Run .\\scripts\\generate-certs.ps1 first " +
        $"(expected: {caPath}).");
}

var trustedClients = new TrustedClientCertificates(certsDir);
builder.Services.AddSingleton(trustedClients);
builder.Services.AddSingleton<CertificateValidation>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
        // Identity is decided by trusted client-*.crt files, not by chain alone.
        httpsOptions.ClientCertificateValidation = (certificate, _, _) =>
        {
            var entry = trustedClients.Find(certificate);
            return entry is not null && entry.NotAfter > DateTimeOffset.UtcNow;
        };
    });
});

builder.Services
    .AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
    .AddCertificate(options =>
    {
        options.AllowedCertificateTypes = CertificateTypes.All;
        options.RevocationMode = X509RevocationMode.NoCheck;
        // Chain trust is only for the demo CA; client identity still comes from client-*.crt.
        options.ChainTrustValidationMode = X509ChainTrustMode.CustomRootTrust;
        options.CustomTrustStore.Add(X509CertificateLoader.LoadCertificateFromFile(caPath));

        options.Events = new CertificateAuthenticationEvents
        {
            OnCertificateValidated = context =>
            {
                var validation = context.HttpContext.RequestServices.GetRequiredService<CertificateValidation>();
                if (!validation.IsAllowed(context.ClientCertificate, out var entry) || entry is null)
                {
                    context.Fail("Client certificate was rejected by the trusted client certificates.");
                    return Task.CompletedTask;
                }

                var fingerprint = CertificateValidation.GetFingerprint(context.ClientCertificate);
                context.Principal = validation.CreatePrincipal(entry, fingerprint);
                context.Success();
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/invoices", (ClaimsPrincipal user) =>
    {
        var clientId = user.FindFirstValue(CertificateValidation.ClientIdClaimType)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Results.Ok(new
        {
            clientId,
            invoices = new[]
            {
                new { id = "INV-1001", amount = 1200.00m, currency = "PLN" },
                new { id = "INV-1002", amount = 450.50m, currency = "PLN" }
            }
        });
    })
    .RequireAuthorization();

app.MapGet("/mtls/check", (ClaimsPrincipal user) =>
    {
        var clientId = user.FindFirstValue(CertificateValidation.ClientIdClaimType)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        var fingerprint = user.FindFirstValue(CertificateValidation.FingerprintClaimType);
        var notAfterRaw = user.FindFirstValue("not_after");

        DateTimeOffset? notAfter = DateTimeOffset.TryParse(notAfterRaw, out var parsed)
            ? parsed
            : null;

        return Results.Ok(new
        {
            clientId,
            fingerprintSha256 = fingerprint,
            notAfter
        });
    })
    .RequireAuthorization();

app.Run();
