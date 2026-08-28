using MongoDB.Driver;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class OnboardingService : IOnboardingService
{
    private readonly MongoDbContext _db;
    private readonly IAuditService _auditService;

    public OnboardingService(MongoDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<OnboardingStatusResponse> GetStatusAsync(Guid institutionId, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        var staffCount = (int)await _db.Users.CountDocumentsAsync(
            u => u.InstitutionId == institutionId && u.Role != UserRole.Admin, cancellationToken: ct);

        var lastEntry = await _db.WalletLedgerEntries
            .Find(e => e.InstitutionId == institutionId)
            .SortByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var walletBalance = lastEntry?.BalanceAfter ?? 0m;

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
                StaffCount = staffCount
            }
        };
    }

    public async Task UpdateInstitutionAsync(Guid institutionId, InstitutionSetupRequest request, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        var update = Builders<Institution>.Update
            .Set(i => i.Name, request.Name)
            .Set(i => i.ContactEmail, request.ContactEmail)
            .Set(i => i.ContactPhone, request.ContactPhone)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);

        if (institution.OnboardingStep < 2)
            update = update.Set(i => i.OnboardingStep, 2);

        await _db.Institutions.UpdateOneAsync(i => i.Id == institutionId, update, cancellationToken: ct);

        await _auditService.LogAsync(AuditAction.Updated, nameof(Institution), institutionId,
            detailsJson: "{\"step\":\"institution_profile\"}", ct: ct);
    }

    public async Task UpdateBusinessInfoAsync(Guid institutionId, BusinessInfoRequest request, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        var update = Builders<Institution>.Update
            .Set(i => i.CacRcNumber, request.CacRcNumber)
            .Set(i => i.Address, request.Address)
            .Set(i => i.ExpectedMonthlyVolume, request.ExpectedMonthlyVolume)
            .Set(i => i.PrimaryUseCase, request.PrimaryUseCase)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);

        if (request.LegalBusinessName != null)
            update = update.Set(i => i.Name, request.LegalBusinessName);
        update = update.Set(i => i.Type, request.Type);

        if (institution.OnboardingStep < 3)
            update = update.Set(i => i.OnboardingStep, 3);

        await _db.Institutions.UpdateOneAsync(i => i.Id == institutionId, update, cancellationToken: ct);

        await _auditService.LogAsync(AuditAction.Updated, nameof(Institution), institutionId,
            detailsJson: "{\"step\":\"business_verification\"}", ct: ct);
    }

    public async Task AcceptComplianceAsync(Guid institutionId, ComplianceAcceptanceRequest request, CancellationToken ct = default)
    {
        if (!request.ResellerAcknowledged || !request.DataProcessingAgreed)
            throw new ArgumentException("Both compliance acknowledgments are required.");

        var institution = await _db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        var update = Builders<Institution>.Update
            .Set(i => i.ComplianceAccepted, true)
            .Set(i => i.ComplianceAcceptedAt, DateTime.UtcNow)
            .Set(i => i.ResellerAcknowledged, request.ResellerAcknowledged)
            .Set(i => i.DataProcessingAgreed, request.DataProcessingAgreed)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);

        if (institution.OnboardingStep < 5)
            update = update.Set(i => i.OnboardingStep, 5);

        await _db.Institutions.UpdateOneAsync(i => i.Id == institutionId, update, cancellationToken: ct);

        await _auditService.LogAsync(AuditAction.Updated, nameof(Institution), institutionId,
            detailsJson: "{\"step\":\"compliance_acknowledgment\"}", ct: ct);
    }

    public async Task<StaffInviteResponse> InviteStaffAsync(Guid institutionId, StaffInviteRequest request, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        var exists = await _db.Users.CountDocumentsAsync(
            u => u.Email == request.Email && u.InstitutionId == institutionId, cancellationToken: ct);
        if (exists > 0)
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
        await _db.Users.InsertOneAsync(user, cancellationToken: ct);

        var instUpdate = Builders<Institution>.Update.Set(i => i.OnboardingStep, 7);
        await _db.Institutions.UpdateOneAsync(i => i.Id == institutionId, instUpdate, cancellationToken: ct);

        await _auditService.LogAsync(AuditAction.Created, nameof(User), user.Id,
            detailsJson: $"{{\"email\":\"{request.Email}\",\"role\":\"{request.Role}\"}}", ct: ct);

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
        var user = await _db.Users.Find(u => u.Id == userId && u.InstitutionId == institutionId).FirstOrDefaultAsync(ct);
        if (user is null) return false;

        if (user.Role == UserRole.SuperAdmin)
            throw new InvalidOperationException("Cannot remove the primary admin.");

        await _db.Users.DeleteOneAsync(u => u.Id == userId, cancellationToken: ct);

        await _auditService.LogAsync(AuditAction.Deleted, nameof(User), userId, ct: ct);
        return true;
    }

    public async Task<List<StaffInviteResponse>> GetStaffAsync(Guid institutionId, CancellationToken ct = default)
    {
        var users = await _db.Users
            .Find(u => u.InstitutionId == institutionId)
            .SortBy(u => u.CreatedAt)
            .ToListAsync(ct);

        return users.Select(u => new StaffInviteResponse
        {
            UserId = u.Id,
            Email = u.Email,
            FullName = u.FullName ?? string.Empty,
            Role = u.Role,
            Status = u.Status
        }).ToList();
    }

    public async Task CompleteOnboardingAsync(Guid institutionId, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        if (!institution.ComplianceAccepted)
            throw new InvalidOperationException("Compliance must be accepted before completing onboarding.");

        var update = Builders<Institution>.Update
            .Set(i => i.OnboardingCompleted, true)
            .Set(i => i.OnboardingStep, 7)
            .Set(i => i.Status, InstitutionStatus.Active)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);

        await _db.Institutions.UpdateOneAsync(i => i.Id == institutionId, update, cancellationToken: ct);

        await _auditService.LogAsync(AuditAction.Updated, nameof(Institution), institutionId,
            detailsJson: "{\"action\":\"onboarding_completed\"}", ct: ct);
    }
}
