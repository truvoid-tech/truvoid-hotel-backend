namespace TruvoID.Domain.Enums;

public enum UserRole
{
    Staff = 0,
    Admin = 1,
    SuperAdmin = 2,
    PlatformAdmin = 3 // TruvoID internal ops — never assigned to institution users
}
