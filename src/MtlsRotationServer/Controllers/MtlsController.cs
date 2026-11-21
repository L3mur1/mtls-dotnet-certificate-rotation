using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MtlsRotationServer.Controllers;

[ApiController]
[Authorize]
[Route("mtls")]
public sealed class MtlsController(ILogger<MtlsController> logger) : ControllerBase
{
    // [Authorize]: needs the authenticated principal (cert claims in User).
    // mTLS itself is already required for the whole API by Kestrel.
    [HttpGet("check")]
    public IActionResult Check()
    {
        try
        {
            var clientId = User.FindFirstValue("client_id")
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException(
                    "Authenticated principal is missing client_id (expected from certificate authentication).");

            var thumbprint = User.FindFirstValue("cert_thumbprint")
                ?? throw new InvalidOperationException(
                    "Authenticated principal is missing cert_thumbprint (expected from certificate authentication).");

            var notAfterRaw = User.FindFirstValue("not_after")
                ?? throw new InvalidOperationException(
                    "Authenticated principal is missing not_after (expected from certificate authentication).");

            if (!DateTimeOffset.TryParse(notAfterRaw, out var notAfter))
            {
                throw new InvalidOperationException(
                    $"Authenticated principal has invalid not_after claim: '{notAfterRaw}'.");
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("mTLS check for client {ClientId}, thumbprint {Thumbprint}", clientId, thumbprint);
            }

            return Ok(new
            {
                clientId,
                thumbprint,
                notAfter
            });
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Failed to return mTLS check response.");
            }

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "server_fault",
                message = "An unexpected error occurred."
            });
        }
    }
}