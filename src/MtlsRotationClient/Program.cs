using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

var which = args.Length > 0 ? args[0] : "current";
var baseUrl = args.Length > 1 ? args[1].TrimEnd('/') : "https://localhost:5001";

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var password = configuration["ClientCertificate:Password"]
    ?? throw new InvalidOperationException("ClientCertificate:Password is missing in appsettings.json.");

var resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");
var clientPfxName = which.Equals("previous", StringComparison.OrdinalIgnoreCase)
    ? "client-previous.pfx"
    : "client-current.pfx";
var clientPfxPath = Path.Combine(resourcesDir, clientPfxName);

if (!File.Exists(clientPfxPath))
{
    throw new InvalidOperationException(
        "Client certificate is missing. Run .\\scripts\\generate-certs.ps1 first " +
        $"(expected: {clientPfxPath}).");
}

using var clientCert = X509CertificateLoader.LoadPkcs12FromFile(clientPfxPath, password);

// Server TLS uses the ASP.NET development certificate (trust via: dotnet dev-certs https --trust).
var handler = new HttpClientHandler
{
    ClientCertificates = { clientCert }
};

using var http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };

Console.WriteLine($"Using {clientPfxName} against {baseUrl}");

await CallAsync(http, "/invoices");
await CallAsync(http, "/mtls/check");

static async Task CallAsync(HttpClient http, string path)
{
    Console.WriteLine();
    Console.WriteLine($"GET {path}");
    using var response = await http.GetAsync(path);
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
    Console.WriteLine(body);
}
