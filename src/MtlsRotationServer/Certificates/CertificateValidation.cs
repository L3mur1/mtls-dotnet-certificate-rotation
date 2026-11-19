using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace MtlsRotationServer.Certificates;

public sealed class CertificateValidation(
    TrustedClientCertificates trustedClients,
    ILogger<CertificateValidation> logger)
{
    public const string ClientIdClaimType = "client_id";
    public const string FingerprintClaimType = "cert_fingerprint";

    public static string GetFingerprint(X509Certificate2 certificate) =>
        TrustedClientCertificates.GetFingerprint(certificate);

    public bool IsAllowed(X509Certificate2 certificate, out RegisteredCertificate? entry)
    {
        entry = trustedClients.Find(certificate);
        var fingerprint = GetFingerprint(certificate);

        if (entry is null)
        {
            logger.LogWarning("Rejecting unknown client certificate {Fingerprint}", fingerprint);
            return false;
        }

        if (entry.NotAfter <= DateTimeOffset.UtcNow)
        {
            logger.LogWarning(
                "Rejecting expired certificate {Fingerprint} for client {ClientId} (notAfter={NotAfter})",
                fingerprint,
                entry.ClientId,
                entry.NotAfter);
            return false;
        }

        return true;
    }

    public ClaimsPrincipal CreatePrincipal(RegisteredCertificate entry, string fingerprint)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, entry.ClientId),
            new Claim(ClientIdClaimType, entry.ClientId),
            new Claim(FingerprintClaimType, fingerprint),
            new Claim("not_after", entry.NotAfter.ToString("O"))
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Certificate"));
    }
}
