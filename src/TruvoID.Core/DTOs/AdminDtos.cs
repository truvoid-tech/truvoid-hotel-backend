using TruvoID.Domain.Enums;

namespace TruvoID.Core.DTOs;

// ─── Admin Overview ───
public record AdminOverviewDto
{
    public decimal RevenueMtd { get; init; }
    public decimal CostsMtd { get; init; }
    public decimal NetMargin { get; init; }
    public int ActiveInstitutions { get; init; }
    public int PendingInstitutions { get; init; }
    public int TotalApiCallsMtd { get; init; }
    public decimal TotalWalletBalances { get; init; }
    public int PendingTopUpApprovals { get; init; }
    public decimal RevenueGrowthPct { get; init; }
    public int NewInstitutionsThisMonth { get; init; }
    public List<InstitutionVolumeDto> TopInstitutions { get; init; } = [];
    public CallBreakdownDto CallBreakdown { get; init; } = new();
}

public record InstitutionVolumeDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public int CallsMtd { get; init; }
    public decimal RevenueMtd { get; init; }
    public bool Active { get; init; }
}

public record CallBreakdownDto
{
    public int NinCalls { get; init; }
    public int BvnCalls { get; init; }
    public int PhoneCalls { get; init; }
    public int Total => NinCalls + BvnCalls + PhoneCalls;
}

// ─── Admin Institutions ───
public record AdminInstitutionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public string Status { get; init; } = "Active";
    public decimal WalletBalance { get; init; }
    public int ApiCallsMtd { get; init; }
    public DateTime JoinedDate { get; init; }
    public string Type { get; init; } = "";
}

// ─── Admin Financials ───
public record AdminFinancialsDto
{
    public decimal GrossRevenue { get; init; }
    public decimal NimcPayouts { get; init; }
    public decimal NetProfit { get; init; }
    public decimal MarginPct { get; init; }
    public int TotalCalls { get; init; }
    public List<AdminTopUpDto> PendingTopUps { get; init; } = [];
    public List<AdminTransactionDto> Transactions { get; init; } = [];
}

public record AdminTopUpDto
{
    public Guid Id { get; init; }
    public string Institution { get; init; } = "";
    public string Email { get; init; } = "";
    public decimal Amount { get; init; }
    public string Reference { get; init; } = "";
    public string Submitted { get; init; } = "";
}

public record AdminTransactionDto
{
    public string Reference { get; init; } = "";
    public string Institution { get; init; } = "";
    public string Type { get; init; } = ""; // Wallet Top-Up, API Call
    public decimal Amount { get; init; }
    public string Date { get; init; } = "";
}

// ─── Admin Pricing ───
public record AdminPricingDto
{
    public string Type { get; init; } = ""; // NIN, BVN, Phone
    public decimal InstitutionCharge { get; init; }
    public decimal NimcCost { get; init; }
    public decimal Margin => InstitutionCharge - NimcCost;
    public decimal MarginPct => InstitutionCharge > 0 ? Math.Round(Margin / InstitutionCharge * 100, 1) : 0;
}

public record UpdatePricingRequest
{
    public decimal InstitutionCharge { get; set; }
    public decimal NimcCost { get; set; }
}
