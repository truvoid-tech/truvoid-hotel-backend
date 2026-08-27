using Microsoft.EntityFrameworkCore;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class OnboardingService : IOnboardingService
{
    private readonly TruvoIDDbContext _db;
    private readonly IAuditService _auditService;

    public OnboardingService(TruvoIDDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<OnboardingStatusResponse> GetStatusAsync(Guid institutionId, CancellationToken ct = default)
    {
        var institution = await _db.Institutions
            .Include(i => i.Users)
            .FirstOrDefaultAsync(i => i.Id == institutionId, ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        var walletBalance = await _db.WalletLedgerEntries
            .Where(e => e.InstitutionId == institutionId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => e.BalanceAfter)
            .FirstOrDefaultAsync(ct);

        return new OnboardingStatusResponse
        {
            CurrentStep = institution.OnboardingStep,
            IsCompleted = institution.OnboardingCompleted,
            Institution = new InstitutionOnboardingInfo
            {
                Id = institution.Id,
                Name = institution.Name,
                Status = institution.Status,
                BusinessInfoSubmitted = !string.IsNullOrEmpty(institution.CacRcNumber),
                ComplianceAccepted = institution.ComplianceAccepted,
                WalletFunded = walletBalance > 0,
                StaffCount = institution.Users.Count(u => u.Role != UserRole.Admin)
            }
        };
    }

    public async Task UpdateInstitutionAsync(Guid institutionId, InstitutionSetupRequest request, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.FindAsync(new object[] { institutionId }, ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        institution.Name = request.Name;
        institution.ContactEmail = request.ContactEmail;
        institution.ContactPhone = request.ContactPhone;
        institution.UpdatedAt = DateTime.UtcNow;

        // Advance onboarding step
        if (institution.OnboardingStep < 2)
            institution.OnboardingStep = 2;

        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            AuditAction.Updated,
            nameof(Institution),
            institutionId,
            detailsJson: "{\"step\":\"institution_profile\"}",
            ct: ct);
    }

    public async Task UpdateBusinessInfoAsync(Guid institutionId, BusinessInfoRequest request, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.FindAsync(new object[] { institutionId }, ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        institution.Name = request.LegalBusinessName ?? institution.Name;
        institution.Type = request.Type;
        institution.CacRcNumber = request.CacRcNumber;
        institution.Address = request.Address;
        institution.ExpectedMonthlyVolume = request.ExpectedMonthlyVolume;
        institution.PrimaryUseCase = request.PrimaryUseCase;
        institution.UpdatedAt = DateTime.UtcNow;

        // Advance onboarding step
        if (institution.OnboardingStep < 3)
            institution.OnboardingStep = 3;

        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            AuditAction.Updated,
            nameof(Institution),
            institutionId,
            detailsJson: "{\"step\":\"business_verification\"}",
            ct: ct);
    }

    public async Task AcceptComplianceAsync(Guid institutionId, ComplianceAcceptanceRequest request, CancellationToken ct = default)
    {
        if (!request.ResellerAcknowledged || !request.DataProcessingAgreed)
            throw new ArgumentException("Both compliance acknowledgments are required.");

        var institution = await _db.Institutions.FindAsync(new object[] { institutionId }, ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        institution.ComplianceAccepted = true;
        institution.ComplianceAcceptedAt = DateTime.UtcNow;
        institution.ResellerAcknowledged = request.ResellerAcknowledged;
        institution.DataProcessingAgreed = request.DataProcessingAgreed;
        institution.UpdatedAt = DateTime.UtcNow;

        // Advance onboarding step
        if (institution.OnboardingStep < 5)
            institution.OnboardingStep = 5;

        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            AuditAction.Updated,
            nameof(Institution),
            institutionId,
            detailsJson: "{\"step\":\"compliance_acknowledgment\"}",
            ct: ct);
    }

    public async Task<StaffInviteResponse> InviteStaffAsync(Guid institutionId, StaffInviteRequest request, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.FindAsync(new object[] { institutionId }, ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        // Check for duplicate email within the institution
        if (await _db.Users.AnyAsync(u => u.Email == request.Email && u.InstitutionId == institutionId, ct))
            throw new InvalidOperationException("A user with this email already exists in this institution.");

        var user = new User
        {
            InstitutionId = institutionId,
            Email = request.Email,
            FullName = request.FullName,
            Role = request.Role,
            Status = UserStatus.PendingInvitation,
            DailyCallLimit = request.DailyCallLimit ?? 50
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // Advance onboarding step
        if (institution.OnboardingStep < 7)
            institution.OnboardingStep = 7;
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            AuditAction.Created,
            nameof(User),
            user.Id,
            detailsJson: $"{{\"email\":\"{request.Email}\",\"role\":\"{request.Role}\"}}",
            ct: ct);

        return new StaffInviteResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName ?? string.Empty,
            Role = user.Role,
            Status = user.Status
        };
    }

    public async Task<bool> RemoveStaffAsync(Guid institutionId, Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.InstitutionId == institutionId, ct);

        if (user is null) return false;

        // Don't allow removing the primary admin
        if (user.Role == UserRole.SuperAdmin)
            throw new InvalidOperationException("Cannot remove the primary admin.");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            AuditAction.Deleted,
            nameof(User),
            userId,
            ct: ct);

        return true;
    }

    public async Task<List<StaffInviteResponse>> GetStaffAsync(Guid institutionId, CancellationToken ct = default)
    {
        return await _db.Users
            .Where(u => u.InstitutionId == institutionId)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new StaffInviteResponse
            {
                UserId = u.Id,
                Email = u.Email,
                FullName = u.FullName ?? string.Empty,
                Role = u.Role,
                Status = u.Status
            })
            .ToListAsync(ct);
    }

    public async Task CompleteOnboardingAsync(Guid institutionId, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.FindAsync(new object[] { institutionId }, ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        // Validate all required steps are done
        if (!institution.ComplianceAccepted)
            throw new InvalidOperationException("Compliance must be accepted before completing onboarding.");

        institution.OnboardingCompleted = true;
        institution.OnboardingStep = 7;
        institution.Status = InstitutionStatus.Active;
        institution.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            AuditAction.Updated,
            nameof(Institution),
            institutionId,
            detailsJson: "{\"action\":\"onboarding_completed\"}",
            ct: ct);
    }
}
