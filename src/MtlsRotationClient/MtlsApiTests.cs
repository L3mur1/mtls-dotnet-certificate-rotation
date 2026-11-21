using System.Net;
using System.Text.Json.Serialization;

namespace MtlsRotationClient;

public sealed class MtlsApiTests(MtlsTestFixture fixture) : IClassFixture<MtlsTestFixture>
{
    [Theory]
    [InlineData("client-current.pfx")]
    [InlineData("client-previous.pfx")]
    public async Task Invoices_accepts_trusted_client_certificate(string pfxFileName)
    {
        using var client = fixture.CreateClient(pfxFileName);

        using var response = await client.GetAsync("/invoices");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Expected OK but got {(int)response.StatusCode}: {body}");
        var payload = await response.Content.ReadFromJsonAsync<InvoicesResponse>();
        Assert.NotNull(payload);
        Assert.Equal("acme-payments", payload.ClientId);
        Assert.NotNull(payload.Invoices);
        Assert.NotEmpty(payload.Invoices);
    }

    [Theory]
    [InlineData("client-current.pfx")]
    [InlineData("client-previous.pfx")]
    public async Task MtlsCheck_reports_client_identity(string pfxFileName)
    {
        using var client = fixture.CreateClient(pfxFileName);

        using var response = await client.GetAsync("/mtls/check");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Expected OK but got {(int)response.StatusCode}: {body}");
        var payload = await response.Content.ReadFromJsonAsync<MtlsCheckResponse>();
        Assert.NotNull(payload);
        Assert.Equal("acme-payments", payload.ClientId);
        Assert.False(string.IsNullOrWhiteSpace(payload.FingerprintSha256));
        Assert.True(payload.NotAfter > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Api_rejects_request_without_client_certificate()
    {
        using var handler = new HttpClientHandler();
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(fixture.BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };

        try
        {
            using var response = await client.GetAsync("/invoices");
            Assert.True(
                response.StatusCode is HttpStatusCode.BadRequest
                    or HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden,
                $"Expected auth failure but got {(int)response.StatusCode}");
        }
        catch (HttpRequestException)
        {
            // Kestrel may abort the TLS handshake when a client certificate is required.
        }
    }

    private sealed class InvoicesResponse
    {
        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; }

        [JsonPropertyName("invoices")]
        public object[]? Invoices { get; set; }
    }

    private sealed class MtlsCheckResponse
    {
        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; }

        [JsonPropertyName("fingerprintSha256")]
        public string? FingerprintSha256 { get; set; }

        [JsonPropertyName("notAfter")]
        public DateTimeOffset NotAfter { get; set; }
    }
}