using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MtlsRotationServer.Certificates;

/// <summary>
/// Allowlist built from public client certificates in Resources (client-*.crt).
/// </summary>
public sealed class TrustedClientCertificates
{
    private readonly Dictionary<string, RegisteredCertificate> _byFingerprint;

    public TrustedClientCertificates(string resourcesDirectory)
    {
        _byFingerprint = new Dictionary<string, RegisteredCertificate>(StringComparer.Ordinal);

        if (!Directory.Exists(resourcesDirectory))
        {
            throw new InvalidOperationException(
                $"Resources directory is missing: {resourcesDirectory}. Run .\\scripts\\generate-certs.ps1 first.");
        }

        foreach (var path in Directory.GetFiles(resourcesDirectory, "client-*.crt"))
        {
            using var certificate = X509CertificateLoader.LoadCertificateFromFile(path);
            var fingerprint = GetFingerprint(certificate);
            var clientId = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException($"Client certificate '{path}' has no CN / simple name.");
            }

            _byFingerprint[fingerprint] = new RegisteredCertificate
            {
                ClientId = clientId,
                FingerprintSha256 = fingerprint,
                NotAfter = certificate.NotAfter.ToUniversalTime()
            };
        }

        if (_byFingerprint.Count == 0)
        {
            throw new InvalidOperationException(
                $"No client-*.crt files found in {resourcesDirectory}. Run .\\scripts\\generate-certs.ps1 first.");
        }
    }

    public static string GetFingerprint(X509Certificate2 certificate) =>
        certificate.GetCertHashString(HashAlgorithmName.SHA256).ToUpperInvariant();

    public RegisteredCertificate? FindByFingerprint(string fingerprintSha256) =>
        _byFingerprint.GetValueOrDefault(fingerprintSha256);

    public RegisteredCertificate? Find(X509Certificate2 certificate) =>
        FindByFingerprint(GetFingerprint(certificate));
}
