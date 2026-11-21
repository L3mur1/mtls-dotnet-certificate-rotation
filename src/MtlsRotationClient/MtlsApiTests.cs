using System.Text.Json;
using System.Text.Json.Nodes;

namespace MtlsRotationClient;

public sealed class MtlsApiTests(MtlsTestFixture fixture) : IClassFixture<MtlsTestFixture>
{
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    [Fact]
    public async Task Api_rejects_request_without_client_certificate()
    {
        // Arrange
        using var client = fixture.CreateBasicClient();

        // Act && Assert — exact TLS error text is OS/.NET-specific; only the failure matters.
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
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        await Verify(new
        {
            statusCode = (int)response.StatusCode,
            body = FormatJson(body)
        })
            .UseParameters(pfxFileName);
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
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        await Verify(new
        {
            statusCode = (int)response.StatusCode,
            body = FormatJson(body, scrub: ["thumbprint", "notAfter"])
        })
            .UseParameters(pfxFileName);
    }

    private static string FormatJson(string json, string[]? scrub = null)
    {
        var node = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("Response body was not valid JSON.");

        if (scrub is not null && node is JsonObject obj)
        {
            foreach (var name in scrub)
            {
                if (obj.ContainsKey(name))
                {
                    obj[name] = "{Scrubbed}";
                }
            }
        }

        return node.ToJsonString(PrettyJson);
    }
}