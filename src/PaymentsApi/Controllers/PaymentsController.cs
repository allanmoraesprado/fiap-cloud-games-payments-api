using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentsApi.Payments;

namespace PaymentsApi.Controllers;

// Payment history queries (Phase 3). Reached through Kong (/api/payments, JWT validated at the
// edge) and validated again here; ownership/role rules are enforced by PaymentQueryService.
[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly PaymentQueryService _queries;

    public PaymentsController(PaymentQueryService queries) => _queries = queries;

    [HttpGet("order/{orderId:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetByOrder(Guid orderId, CancellationToken ct)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(idClaim, out var callerId))
            return Unauthorized();

        var result = await _queries.GetByOrderAsync(orderId, callerId, User.IsInRole("Admin"), ct);

        return result.Outcome switch
        {
            PaymentLookupOutcome.Found => Ok(result.Payment),
            PaymentLookupOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden,
                new { status = 403, error = "Forbidden", message = "You can only view your own payments." }),
            _ => NotFound(new { status = 404, error = "NotFound", message = "No payment found for this order." })
        };
    }
}
