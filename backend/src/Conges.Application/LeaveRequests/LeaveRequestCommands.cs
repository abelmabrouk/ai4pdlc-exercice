using Conges.Application.Common;
using Conges.Domain.LeaveRequests;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Conges.Application.LeaveRequests;

/// <remarks>Champs nullables : un champ absent est une erreur de forme (400), et non une date par défaut.</remarks>
public sealed record SubmitLeaveRequest(LeaveType? Type, DateOnly? StartDate, DateOnly? EndDate, string? Comment);

public sealed record RejectLeaveRequest(string? Reason);

/// <summary>
/// Validation de forme uniquement. Les règles de gestion (ordre des dates, prévenance, solde…) sont contrôlées par
/// le service et renvoient un code métier (ADR-003).
/// </summary>
public sealed class SubmitLeaveRequestValidator : AbstractValidator<SubmitLeaveRequest>
{
    public SubmitLeaveRequestValidator()
    {
        RuleFor(r => r.Type).NotNull().IsInEnum();
        RuleFor(r => r.StartDate).NotNull();
        RuleFor(r => r.EndDate).NotNull();
    }
}

public interface ILeaveRequestCommands
{
    Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequest request, CancellationToken ct);
    Task<LeaveRequestDto> ApproveAsync(int id, CancellationToken ct);
    Task<LeaveRequestDto> RejectAsync(int id, string? reason, CancellationToken ct);
}

internal sealed class LeaveRequestCommands(
    IAppDbContext db,
    ICurrentUser currentUser,
    TimeProvider clock,
    LeaveDayCounter dayCounter,
    IValidator<SubmitLeaveRequest> validator) : ILeaveRequestCommands
{
    /// <summary>Délai de prévenance des congés payés, en jours ouvrés (RG-03).</summary>
    private const int NoticeBusinessDays = 2;

    /// <summary>Longueur minimale du motif de refus, espaces de début et de fin exclus (RG-13).</summary>
    private const int MinRejectionReasonLength = 10;

    public async Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var (type, start, end) = (request.Type!.Value, request.StartDate!.Value, request.EndDate!.Value);

        var employeeId = currentUser.EmployeeId;
        var employee = await db.Employees.Include(e => e.Team).SingleOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new NotFoundException("Employé", employeeId);

        // RG-01
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        if (end < start)
            throw new BusinessRuleException(LeaveErrors.InvalidPeriod, "La date de fin précède la date de début.");
        if (start < today)
            throw new BusinessRuleException(LeaveErrors.InvalidPeriod, "La date de début est dans le passé.");

        // RG-02
        var days = dayCounter.CountLeaveDays(start, end);
        if (days == 0)
            throw new BusinessRuleException(LeaveErrors.InvalidPeriod, "La période ne contient aucun jour ouvrable.");

        // RG-03, RG-04
        if (type == LeaveType.PaidLeave && dayCounter.CountWorkingDaysBetween(today, start) < NoticeBusinessDays)
            throw new BusinessRuleException(LeaveErrors.NoticeTooShort,
                $"Les congés payés doivent être posés au moins {NoticeBusinessDays} jours ouvrés à l'avance.");

        // RG-06
        var overlaps = await db.LeaveRequests.AnyAsync(r =>
            r.EmployeeId == employeeId
            && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved)
            && r.StartDate <= end && r.EndDate >= start, ct);
        if (overlaps)
            throw new BusinessRuleException(LeaveErrors.Overlap, "La période chevauche une autre demande en attente ou approuvée.");

        // RG-05
        if (type == LeaveType.PaidLeave)
        {
            var pendingDays = await db.LeaveRequests
                .Where(r => r.EmployeeId == employeeId && r.Type == LeaveType.PaidLeave && r.Status == LeaveStatus.Pending)
                .SumAsync(r => r.Days, ct);
            var available = employee.PaidLeaveBalance - pendingDays;
            if (days > available)
                throw new BusinessRuleException(LeaveErrors.InsufficientBalance, $"{days} jours demandés, {available} jours disponibles.");
        }

        // RG-07
        var leaveRequest = new LeaveRequest
        {
            EmployeeId = employeeId,
            Employee = employee,
            Type = type,
            StartDate = start,
            EndDate = end,
            Days = days,
            Status = LeaveStatus.Pending,
            Comment = request.Comment,
            SubmittedAt = clock.GetUtcNow(),
        };
        db.LeaveRequests.Add(leaveRequest);
        await db.SaveChangesAsync(ct);

        return leaveRequest.ToDto();
    }

    public async Task<LeaveRequestDto> ApproveAsync(int id, CancellationToken ct)
    {
        var request = await LoadForDecisionAsync(id, ct);

        // RG-12 : le solde CP est débité à l'approbation, jamais à la soumission.
        request.Status = LeaveStatus.Approved;
        if (request.Type == LeaveType.PaidLeave) request.Employee!.PaidLeaveBalance -= request.Days;

        return await SaveDecisionAsync(request, ct);
    }

    public async Task<LeaveRequestDto> RejectAsync(int id, string? reason, CancellationToken ct)
    {
        var request = await LoadForDecisionAsync(id, ct);

        // RG-13 : règle de gestion (422 avec code), et non validation de forme.
        var trimmed = reason?.Trim() ?? "";
        if (trimmed.Length < MinRejectionReasonLength)
            throw new BusinessRuleException(LeaveErrors.RejectionReasonRequired,
                $"Le motif du refus doit contenir au moins {MinRejectionReasonLength} caractères.");

        request.Status = LeaveStatus.Rejected;
        request.RejectionReason = trimmed;

        return await SaveDecisionAsync(request, ct);
    }

    /// <summary>Charge une demande à décider et vérifie que l'utilisateur courant peut la décider (RG-10, RG-11).</summary>
    private async Task<LeaveRequest> LoadForDecisionAsync(int id, CancellationToken ct)
    {
        var deciderId = currentUser.EmployeeId; // 401 avant tout autre contrôle

        var request = await db.LeaveRequests
            .Include(r => r.Employee!).ThenInclude(e => e.Team!).ThenInclude(t => t.ParentTeam)
            .SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new BusinessRuleException(LeaveErrors.NotFound, $"La demande {id} n'existe pas.");

        if (ApprovalCircuit.ApproverOf(request.Employee!) != deciderId)
            throw new BusinessRuleException(LeaveErrors.ApproverNotAllowed, "Vous n'êtes pas le valideur de cette demande.");

        if (request.Status != LeaveStatus.Pending)
            throw new BusinessRuleException(LeaveErrors.AlreadyDecided, "La demande a déjà été décidée.");

        return request;
    }

    /// <summary>Trace la décision (RG-14) et l'enregistre en une seule opération.</summary>
    private async Task<LeaveRequestDto> SaveDecisionAsync(LeaveRequest request, CancellationToken ct)
    {
        request.DecidedAt = clock.GetUtcNow();
        request.DecidedById = currentUser.EmployeeId;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Une autre décision a été enregistrée entre la lecture et l'écriture.
            throw new BusinessRuleException(LeaveErrors.AlreadyDecided, "La demande a déjà été décidée.");
        }

        return request.ToDto();
    }
}
