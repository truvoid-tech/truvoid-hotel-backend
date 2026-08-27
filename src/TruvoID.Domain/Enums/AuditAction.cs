namespace TruvoID.Domain.Enums;

public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
    Verified = 3,
    WalletCredited = 4,
    WalletDebited = 5,
    WalletReversed = 6,
    ApiKeyGenerated = 7,
    ApiKeyRevoked = 8,
    Login = 9,
    Logout = 10
}
