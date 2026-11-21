using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MtlsRotationServer.Controllers;

[ApiController]
[Authorize]
[Route("invoices")]
public sealed class InvoicesController(ILogger<InvoicesController> logger) : ControllerBase
{
    // [Authorize]: needs the authenticated principal (cert claims in User).
    // mTLS itself is already required for the whole API by Kestrel.
    [HttpGet]
    public IActionResult Get()
    {
        try
        {
            var clientId = User.FindFirstValue("client_id")
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException(
                    "Authenticated principal is missing client_id (expected from certificate authentication).");

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Returning invoices for client {ClientId}", clientId);
            }

            return Ok(new
            {
                clientId,
                invoices = new[]
                {
                    new { id = "INV-1001", amount = 1200.00m, currency = "PLN" },
                    new { id = "INV-1002", amount = 450.50m, currency = "PLN" }
                }
            });
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Failed to return invoices.");
            }

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "server_fault",
                message = "An unexpected error occurred."
            });
        }
    }
}
