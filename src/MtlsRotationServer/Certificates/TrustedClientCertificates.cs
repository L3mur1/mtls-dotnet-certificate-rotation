using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MtlsRotationServer.Certificates;

/// <summary>
/// Allowlist built from public client certificate files (client-*.crt).
/// These are public certs only (no private key, no password).
/// </summary>
public sealed class TrustedClientCertificates
{
    private readonly Dictionary<string, RegisteredCertificate> _trustedByFingerprint;

    public TrustedClientCertificates(IEnumerable<string> certificatePaths)
    {
        _trustedByFingerprint = new Dictionary<string, RegisteredCertificate>(StringComparer.Ordinal);

        foreach (var path in certificatePaths)
        {
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Client certificate is missing: {path}. Run .\\scripts\\generate-certs.ps1 first.");
            }

            using var certificate = X509CertificateLoader.LoadCertificateFromFile(path);
            var fingerprint = GetFingerprint(certificate);
            var clientId = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException($"Client certificate '{path}' has no CN / simple name.");
            }

            _trustedByFingerprint[fingerprint] = new RegisteredCertificate
            {
                ClientId = clientId,
                FingerprintSha256 = fingerprint,
                NotAfter = certificate.NotAfter.ToUniversalTime()
            };
        }

        if (_trustedByFingerprint.Count == 0)
        {
            throw new InvalidOperationException(
                "No client certificates were provided. Run .\\scripts\\generate-certs.ps1 first.");
        }
    }

    public static string GetFingerprint(X509Certificate2 certificate) =>
        certificate.GetCertHashString(HashAlgorithmName.SHA256).ToUpperInvariant();

    public RegisteredCertificate? FindByFingerprint(string fingerprintSha256) =>
        _trustedByFingerprint.GetValueOrDefault(fingerprintSha256);

    public RegisteredCertificate? Find(X509Certificate2 certificate) =>
        FindByFingerprint(GetFingerprint(certificate));
}
