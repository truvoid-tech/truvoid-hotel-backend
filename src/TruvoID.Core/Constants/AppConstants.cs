namespace TruvoID.Core.Constants;

/// <summary>
/// Application-wide constants for TruvoID.
/// </summary>
public static class AppConstants
{
    /// <summary>API key prefix for live keys.</summary>
    public const string LiveKeyPrefix = "tv_live_";
    
    /// <summary>API key prefix for test keys.</summary>
    public const string TestKeyPrefix = "tv_test_";
    
    /// <summary>Default low-balance alert threshold in NGN.</summary>
    public const decimal DefaultLowBalanceThreshold = 1000.00m;
    
    /// <summary>Redis key prefix for rate limiting.</summary>
    public const string RateLimitKeyPrefix = "ratelimit:";
    
    /// <summary>Redis key prefix for wallet balance cache.</summary>
    public const string WalletBalanceKeyPrefix = "wallet:balance:";
    
    /// <summary>Redis key prefix for idempotency.</summary>
    public const string IdempotencyKeyPrefix = "idempotency:";
}
