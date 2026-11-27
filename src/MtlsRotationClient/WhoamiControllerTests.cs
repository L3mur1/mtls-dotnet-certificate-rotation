using System.Net;
using System.Text.Json.Nodes;

namespace MtlsRotationClient;

public sealed class WhoamiControllerTests(MtlsTestFixture fixture) : IClassFixture<MtlsTestFixture>
{
    [Fact]
    public async Task GetWhoami_WhenClientCertificateFromUnknownCa_ShouldRejectConnection()
    {
        // Arrange — same CN as trusted clients, but issued by an unknown CA and not on the allowlist.
        using var client = fixture.CreateMtlsClient("acme-payments-unknown-ca.pfx");

        // Act && Assert — rejected at TLS (thumbprint / chain), before HTTP.
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => client.GetAsync("/whoami", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetWhoami_WhenClientCertificateMissing_ShouldRejectConnection()
    {
        // Arrange
        using var client = fixture.CreateBasicClient();

        // Act && Assert — exact TLS error text is OS/.NET-specific; only the failure matters.
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => client.GetAsync("/whoami", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetWhoami_WhenClientCertificateNotOnAllowlist_ShouldRejectConnection()
    {
        // Arrange — trusted CA (ca-current), same CN, but thumbprint is not on the server allowlist.
        using var client = fixture.CreateMtlsClient("acme-payments-unlisted.pfx");

        // Act && Assert — rejected at TLS (allowlist), before HTTP.
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => client.GetAsync("/whoami", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("acme-payments-current.pfx")]
    [InlineData("acme-payments-previous.pfx")]
    public async Task GetWhoami_WhenClientIsTrusted_ShouldReturnClientIdentity(string pfxFileName)
    {
        // Arrange
        using var expectedCert = fixture.LoadClientCertificate(pfxFileName);
        using var client = fixture.CreateMtlsClient(pfxFileName);

        // Act
        using var response = await client.GetAsync("/whoami", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonNode>(TestContext.Current.CancellationToken);

        // Assert — identity and thumbprint match the public cert inside the PFX we presented.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        Assert.Equal("acme-payments", (string?)body["clientId"]);
        Assert.Equal(expectedCert.Thumbprint, (string?)body["thumbprint"], StringComparer.OrdinalIgnoreCase);

        var notAfter = body["notAfter"]!.GetValue<DateTimeOffset>();
        Assert.Equal(
            DateTime.SpecifyKind(expectedCert.NotAfter, DateTimeKind.Local).ToUniversalTime(),
            notAfter.UtcDateTime,
            TimeSpan.FromSeconds(1));
    }
}