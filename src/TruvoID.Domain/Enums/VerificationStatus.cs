namespace TruvoID.Domain.Enums;

public enum VerificationStatus
{
    Pending = 0,
    Match = 1,
    NoMatch = 2,
    Error = 3,
    Reversed = 4
}
