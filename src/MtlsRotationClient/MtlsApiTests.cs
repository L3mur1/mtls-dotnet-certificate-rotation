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
    public async Task Api_rejects_untrusted_client_certificate()
    {
        // Arrange — same CN as trusted clients, but issued by an unknown CA and not on the allowlist.
        using var client = fixture.CreateMtlsClient("client-untrusted.pfx");

        // Act && Assert — rejected at TLS (thumbprint / chain), before HTTP.
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => client.GetAsync("/invoices", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("client-current.pfx")]
    [InlineData("client-previous.pfx")]
    public async Task Invoices_accepts_trusted_client_certificate(string pfxFileName)
    {
        // Arrange
        using var client = fixture.CreateMtlsClient(pfxFileName);

        // Act
        using var response = await client.GetAsync("/invoices", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<InvoicesResponse>(JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("acme-payments", body.ClientId);
        Assert.Equal(2, body.Invoices.Count);
        Assert.Equal("INV-1001", body.Invoices[0].Id);
        Assert.Equal(1200.00m, body.Invoices[0].Amount);
        Assert.Equal("PLN", body.Invoices[0].Currency);
        Assert.Equal("INV-1002", body.Invoices[1].Id);
        Assert.Equal(450.50m, body.Invoices[1].Amount);
        Assert.Equal("PLN", body.Invoices[1].Currency);
    }

    [Theory]
    [InlineData("client-current.pfx")]
    [InlineData("client-previous.pfx")]
    public async Task MtlsCheck_reports_client_identity(string pfxFileName)
    {
        // Arrange
        using var client = fixture.CreateMtlsClient(pfxFileName);

        // Act
        using var response = await client.GetAsync("/mtls/check", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<MtlsCheckResponse>(JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("acme-payments", body.ClientId);
        Assert.False(string.IsNullOrWhiteSpace(body.Thumbprint));
        Assert.True(body.NotAfter > DateTimeOffset.UtcNow);
    }

    private sealed record InvoicesResponse(string ClientId, IReadOnlyList<InvoiceDto> Invoices);

    private sealed record InvoiceDto(string Id, decimal Amount, string Currency);

    private sealed record MtlsCheckResponse(string ClientId, string Thumbprint, DateTimeOffset NotAfter);
}