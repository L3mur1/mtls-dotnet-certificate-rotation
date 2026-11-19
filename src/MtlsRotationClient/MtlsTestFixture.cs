using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace MtlsRotationClient;

/// <summary>
/// Live HTTPS calls against a running MtlsRotationServer (real mTLS).
/// Start the API first: .\scripts\Start-Api.ps1
/// </summary>
public sealed class MtlsTestFixture : IDisposable
{
    public string BaseUrl { get; }
    public string Password { get; }
    public string ResourcesDir { get; }

    public MtlsTestFixture()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        BaseUrl = (configuration["BaseUrl"] ?? "https://localhost:5001").TrimEnd('/');
        Password = configuration["ClientCertificate:Password"]
            ?? throw new InvalidOperationException("ClientCertificate:Password is missing in appsettings.json.");
        ResourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");
    }

    public HttpClient CreateClient(string pfxFileName)
    {
        var pfxPath = Path.Combine(ResourcesDir, pfxFileName);
        if (!File.Exists(pfxPath))
        {
            throw new InvalidOperationException(
                $"Client certificate is missing: {pfxPath}. Run .\\scripts\\generate-certs.ps1 first.");
        }

        var clientCert = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, Password);
        var handler = new HttpClientHandler
        {
            ClientCertificates = { clientCert }
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public void Dispose()
    {
    }
}
