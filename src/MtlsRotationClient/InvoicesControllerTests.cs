using System.Net;
using System.Text.Json.Nodes;

namespace MtlsRotationClient;

public sealed class InvoicesControllerTests(MtlsTestFixture fixture) : IClassFixture<MtlsTestFixture>
{
    [Fact]
    public async Task GetInvoices_WhenClientCertificateFromUnknownCa_ShouldRejectConnection()
    {
        // Arrange — same CN as trusted clients, but issued by an unknown CA and not on the allowlist.
        using var client = fixture.CreateMtlsClient("acme-payments-unknown-ca.pfx");

        // Act && Assert — rejected at TLS (thumbprint / chain), before HTTP.
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => client.GetAsync("/invoices", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetInvoices_WhenClientCertificateMissing_ShouldRejectConnection()
    {
        // Arrange
        using var client = fixture.CreateBasicClient();

        // Act && Assert — exact TLS error text is OS/.NET-specific; only the failure matters.
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => client.GetAsync("/invoices", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetInvoices_WhenClientCertificateNotOnAllowlist_ShouldRejectConnection()
    {
        // Arrange — trusted CA (ca-current), same CN, but thumbprint is not on the server allowlist.
        using var client = fixture.CreateMtlsClient("acme-payments-unlisted.pfx");

        // Act && Assert — rejected at TLS (allowlist), before HTTP.
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => client.GetAsync("/invoices", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("acme-payments-current.pfx")]
    [InlineData("acme-payments-previous.pfx")]
    public async Task GetInvoices_WhenClientIsTrusted_ShouldReturnInvoices(string pfxFileName)
    {
        // Arrange
        using var client = fixture.CreateMtlsClient(pfxFileName);

        // Act
        using var response = await client.GetAsync("/invoices", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonNode>(TestContext.Current.CancellationToken);

        // Assert — mTLS + identity; invoice payload is demo data, not under test.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        Assert.Equal("acme-payments", (string?)body["clientId"]);
        Assert.True(body["invoices"] is JsonArray { Count: > 0 });
    }
}