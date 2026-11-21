using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MtlsRotationClient;

public sealed class MtlsApiTests(MtlsTestFixture fixture) : IClassFixture<MtlsTestFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Api_rejects_request_without_client_certificate()
    {
        // Arrange
        using var client = fixture.CreateBasicClient();

        // Act && Assert — exact TLS error text is OS/.NET-specific; only the failure matters.
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => client.GetAsync("/invoices", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Api_rejects_client_certificate_from_unknown_ca()
    {
        // Arrange — same CN as trusted clients, but issued by an unknown CA and not on the allowlist.
        using var client = fixture.CreateMtlsClient("acme-payments-unknown-ca.pfx");

        // Act && Assert — rejected at TLS (thumbprint / chain), before HTTP.
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => client.GetAsync("/invoices", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("acme-payments-current.pfx")]
    [InlineData("acme-payments-previous.pfx")]
    public async Task Invoices_accepts_trusted_client_certificate(string pfxFileName)
    {
        // Arrange
        using var client = fixture.CreateMtlsClient(pfxFileName);

        // Act
        using var response = await client.GetAsync("/invoices", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<InvoicesResponse>(JsonOptions, TestContext.Current.CancellationToken);

        // Assert — mTLS + identity; invoice payload is demo data, not under test.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("acme-payments", body.ClientId);
        Assert.NotEmpty(body.Invoices);
    }

    [Theory]
    [InlineData("acme-payments-current.pfx")]
    [InlineData("acme-payments-previous.pfx")]
    public async Task MtlsCheck_reports_client_identity(string pfxFileName)
    {
        // Arrange
        using var expectedCert = fixture.LoadClientCertificate(pfxFileName);
        using var client = fixture.CreateMtlsClient(pfxFileName);

        // Act
        using var response = await client.GetAsync("/mtls/check", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<MtlsCheckResponse>(JsonOptions, TestContext.Current.CancellationToken);

        // Assert — identity and thumbprint match the public cert inside the PFX we presented.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("acme-payments", body.ClientId);
        Assert.Equal(expectedCert.Thumbprint, body.Thumbprint, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(
            DateTime.SpecifyKind(expectedCert.NotAfter, DateTimeKind.Local).ToUniversalTime(),
            body.NotAfter.UtcDateTime,
            TimeSpan.FromSeconds(1));
    }

    private sealed record InvoicesResponse(string ClientId, IReadOnlyList<JsonElement> Invoices);

    private sealed record MtlsCheckResponse(string ClientId, string Thumbprint, DateTimeOffset NotAfter);
}
