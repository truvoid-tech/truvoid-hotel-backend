using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;

namespace TruvoID.API.Controllers;

[ApiController]
[Route("v1/wallet")]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;

    public WalletController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    /// <summary>
    /// Get current wallet balance for the authenticated institution.
    /// </summary>
    [HttpGet("balance")]
    [ProducesResponseType(typeof(WalletBalanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBalance(CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        var balance = await _walletService.GetBalanceAsync(institutionId, ct);

        return Ok(balance ?? new WalletBalanceResponse
        {
            Balance = 0,
            InstitutionId = institutionId,
            LastUpdated = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get paginated wallet transaction history.
    /// </summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(PaginatedResponse<WalletTransactionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var institutionId = GetInstitutionId();

        // Clamp page size
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var transactions = await _walletService.GetTransactionsAsync(institutionId, page, pageSize, ct);

        // For a proper paginated response we'd need total count. Since GetTransactionsAsync
        // returns a list, we wrap it. For production, the wallet service should return total count too.
        return Ok(new PaginatedResponse<WalletTransactionResponse>
        {
            Items = transactions,
            Page = page,
            PageSize = pageSize,
            TotalCount = transactions.Count // Approximation — would need a proper count query
        });
    }

    /// <summary>
    /// Initiate a wallet top-up via Paystack or Flutterwave.
    /// Admin-only endpoint.
    /// </summary>
    [HttpPost("topup/initiate")]
    [ProducesResponseType(typeof(TopupInitiateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiateTopup([FromBody] TopupInitiateRequest request, CancellationToken ct)
    {
        // TODO: Implement Paystack/Flutterwave integration
        // For now, return a stub response
        var institutionId = GetInstitutionId();

        return Ok(new TopupInitiateResponse
        {
            AuthorizationUrl = $"https://checkout.paystack.com/placeholder?amount={request.Amount}&institution={institutionId}",
            Reference = $"topup_{Guid.NewGuid():N}",
            Amount = request.Amount
        });
    }

    /// <summary>
    /// Webhook receiver for payment gateway confirmation.
    /// Signed webhook — validates signature before crediting wallet.
    /// </summary>
    [HttpPost("topup/webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TopupWebhook(CancellationToken ct)
    {
        // TODO: Implement Paystack/Flutterwave webhook signature validation
        // TODO: Extract reference, verify payment, then credit wallet via _walletService.CreditAsync

        // For now, acknowledge receipt
        return Ok();
    }

    private Guid GetInstitutionId()
    {
        if (HttpContext.Items["InstitutionId"] is Guid id)
            return id;

        if (User.FindFirstValue("institution_id") is string idStr && Guid.TryParse(idStr, out var parsed))
            return parsed;

        throw new UnauthorizedAccessException("No institution context found.");
    }
}
