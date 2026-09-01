namespace TruvoID.Core.DTOs;

public record TokenRateDto
{
    public string Type { get; init; } = "";
    public decimal PricePerCall { get; init; }
    public decimal TokenCost { get; init; }
}

public record TokenPricingResponse
{
    public decimal NairaPerToken { get; init; }
    public List<TokenRateDto> Rates { get; init; } = [];
}

public record TokenEstimateItem
{
    public string Type { get; init; } = "";
    public decimal PricePerCall { get; init; }
    public decimal TokenCost { get; init; }
    public long EstimatedVerifications { get; init; }
}

public record TokenEstimateResponse
{
    public decimal AmountNaira { get; init; }
    public decimal Tokens { get; init; }
    public List<TokenEstimateItem> Estimates { get; init; } = [];
}
