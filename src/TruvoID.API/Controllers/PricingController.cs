using Microsoft.AspNetCore.Mvc;
using TruvoID.Core.Constants;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;

namespace TruvoID.API.Controllers;

[ApiController]
[Route("v1/pricing")]
public class PricingController : ControllerBase
{
    private readonly IPricingService _pricingService;

    public PricingController(IPricingService pricingService)
    {
        _pricingService = pricingService;
    }

    /// <summary>
    /// Get the global token conversion rate and token cost per verification type.
    /// </summary>
    [HttpGet("tokens")]
    [ProducesResponseType(typeof(TokenPricingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTokenPricing(CancellationToken ct)
    {
        var rates = new List<TokenRateDto>();

        foreach (var type in Enum.GetValues<VerificationType>())
        {
            decimal price;
            try
            {
                price = await _pricingService.GetPriceAsync(type, institutionId: null, ct);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            rates.Add(new TokenRateDto
            {
                Type = type.ToString(),
                PricePerCall = price,
                TokenCost = Math.Ceiling(price / TokenPricing.NairaPerToken)
            });
        }

        return Ok(new TokenPricingResponse
        {
            NairaPerToken = TokenPricing.NairaPerToken,
            Rates = rates
        });
    }

    /// <summary>
    /// Estimate how many tokens and verifications a given Naira amount would buy.
    /// </summary>
    [HttpGet("estimate")]
    [ProducesResponseType(typeof(TokenEstimateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTokenEstimate([FromQuery] decimal amount, CancellationToken ct)
    {
        if (amount <= 0)
            return BadRequest(new { code = "INVALID_INPUT", message = "Amount must be greater than zero." });

        var tokens = Math.Floor(amount / TokenPricing.NairaPerToken);
        var estimates = new List<TokenEstimateItem>();

        foreach (var type in Enum.GetValues<VerificationType>())
        {
            decimal price;
            try
            {
                price = await _pricingService.GetPriceAsync(type, institutionId: null, ct);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            var tokenCost = Math.Ceiling(price / TokenPricing.NairaPerToken);
            estimates.Add(new TokenEstimateItem
            {
                Type = type.ToString(),
                PricePerCall = price,
                TokenCost = tokenCost,
                EstimatedVerifications = tokenCost > 0 ? (long)(tokens / tokenCost) : 0
            });
        }

        return Ok(new TokenEstimateResponse
        {
            AmountNaira = amount,
            Tokens = tokens,
            Estimates = estimates
        });
    }
}
