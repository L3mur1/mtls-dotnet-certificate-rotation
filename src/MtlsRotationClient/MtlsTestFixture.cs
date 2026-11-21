using Microsoft.Extensions.Configuration;
using System.Security.Cryptography.X509Certificates;

namespace MtlsRotationClient;

public sealed class MtlsTestFixture : IDisposable
{
    private const int RequestTimeoutSeconds = 10;
    public string BaseUrl { get; }
    public string Password { get; }
    public string ResourcesDir { get; }

    public MtlsTestFixture()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        BaseUrl = configuration["BaseUrl"]
            ?? throw new InvalidOperationException("BaseUrl is missing in appsettings.json.");

        Password = configuration["ClientCertificate:Password"]
            ?? throw new InvalidOperationException("ClientCertificate:Password is missing in appsettings.json.");

        ResourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources", "ClientCerts");
    }

    public HttpClient CreateBasicClient() => new HttpClient
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
    };

    public HttpClient CreateMtlsClient(string pfxFileName)
    {
        var clientCert = LoadClientCertificate(pfxFileName);
        var handler = new HttpClientHandler
        {
            ClientCertificates = { clientCert },
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
        };
    }

    public X509Certificate2 LoadClientCertificate(string pfxFileName)
    {
        var pfxPath = Path.Combine(ResourcesDir, pfxFileName);
        if (!File.Exists(pfxPath))
        {
            throw new InvalidOperationException(
                $"Client certificate is missing: {pfxPath}. Run .\\scripts\\generate-certs.ps1 to generate it.");
        }

        return X509CertificateLoader.LoadPkcs12FromFile(pfxPath, Password);
    }

    public void Dispose()
    {
    }
}
