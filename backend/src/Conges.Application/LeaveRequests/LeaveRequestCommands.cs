using Conges.Application.Common;
using Conges.Domain.LeaveRequests;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Conges.Application.LeaveRequests;

/// <summary>Soumission d'une demande. Champs nullables pour détecter leur absence (validation de forme).</summary>
public sealed record SubmitLeaveRequest(LeaveType? Type, DateOnly? StartDate, DateOnly? EndDate, string? Comment);

/// <summary>
/// Validation de <b>forme</b> uniquement. Les règles de gestion (RG-01 à RG-06) sont vérifiées par le service
/// et produisent un code métier : elles ne doivent jamais figurer ici (ADR-003).
/// </summary>
public sealed class SubmitLeaveRequestValidator : AbstractValidator<SubmitLeaveRequest>
{
    public SubmitLeaveRequestValidator()
    {
        RuleFor(r => r.Type).NotNull().IsInEnum().WithMessage("Le type de congé est obligatoire (PaidLeave ou FamilyEvent).");
        RuleFor(r => r.StartDate).NotNull().WithMessage("La date de début est obligatoire.");
        RuleFor(r => r.EndDate).NotNull().WithMessage("La date de fin est obligatoire.");
    }
}

/// <summary>
/// Refus d'une demande. Aucun validateur de forme : l'absence ou la brièveté du motif relève de RG-13 (422).
/// </summary>
public sealed record RejectLeaveRequest(string? Reason);

public interface ILeaveRequestCommands
{
    /// <summary>Soumet une demande pour l'utilisateur courant (RG-01 à RG-07).</summary>
    Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequest request, CancellationToken ct);

    /// <summary>Approuve une demande en attente dont l'utilisateur courant est le valideur (RG-10 à RG-12, RG-14).</summary>
    Task<LeaveRequestDto> ApproveAsync(int id, CancellationToken ct);

    /// <summary>Refuse une demande en attente avec un motif (RG-10, RG-11, RG-13, RG-14).</summary>
    Task<LeaveRequestDto> RejectAsync(int id, RejectLeaveRequest? request, CancellationToken ct);
}

internal sealed class LeaveRequestCommands(
    IAppDbContext db,
    ICurrentUser currentUser,
    ILeaveRequestQueries queries,
    LeaveDayCounter dayCounter,
    TimeProvider clock,
    IValidator<SubmitLeaveRequest> submitValidator) : ILeaveRequestCommands
{
    /// <summary>RG-13 : longueur minimale du motif de refus, espaces de début et de fin exclus.</summary>
    private const int MinRejectionReasonLength = 10;

    public async Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequest request, CancellationToken ct)
    {
        await submitValidator.ValidateAndThrowAsync(request, ct);

        var start = request.StartDate!.Value;
        var end = request.EndDate!.Value;
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

        // RG-01 : période continue, qui ne commence pas dans le passé.
        if (start > end || start < today) throw LeaveErrors.InvalidPeriod();

        // RG-02 : décompte en jours ouvrables ; une période sans jour décompté est invalide.
        var days = dayCounter.CountLeaveDays(start, end);
        if (days == 0) throw LeaveErrors.InvalidPeriod();

        var type = request.Type!.Value;

        // RG-03 : au moins 2 jours ouvrés de prévenance pour les congés payés.
        // RG-04 : pas de délai de prévenance pour les événements familiaux.
        if (type == LeaveType.PaidLeave && dayCounter.CountNoticeDays(today, start) < 2)
        {
            throw LeaveErrors.NoticeTooShort();
        }

        var employeeId = currentUser.EmployeeId;

        // RG-05 : pour les congés payés, le solde disponible (solde CP moins les jours des autres demandes CP en
        // attente) doit couvrir la demande. Le solde n'est pas modifié à la soumission (débit à l'approbation).
        if (type == LeaveType.PaidLeave)
        {
            var balance = await db.Employees
                .Where(e => e.Id == employeeId)
                .Select(e => e.PaidLeaveBalance)
                .SingleAsync(ct);
            var pendingDays = await db.LeaveRequests
                .Where(r => r.EmployeeId == employeeId && r.Type == LeaveType.PaidLeave && r.Status == LeaveStatus.Pending)
                .SumAsync(r => r.Days, ct);
            var available = balance - pendingDays;

            if (days > available) throw LeaveErrors.InsufficientBalance(days, available);
        }

        // RG-06 : pas de jour commun avec une autre demande du collaborateur en attente ou approuvée.
        var overlaps = await db.LeaveRequests.AnyAsync(r =>
            r.EmployeeId == employeeId
            && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved)
            && r.StartDate <= end && r.EndDate >= start, ct);
        if (overlaps) throw LeaveErrors.Overlap();

        var leave = new LeaveRequest
        {
            EmployeeId = employeeId,
            Type = type,
            StartDate = start,
            EndDate = end,
            Days = days,
            Status = LeaveStatus.Pending,
            Comment = request.Comment,
            SubmittedAt = clock.GetUtcNow(),
        };

        db.LeaveRequests.Add(leave);
        await db.SaveChangesAsync(ct);

        return await queries.GetAsync(leave.Id, ct);
    }

    public async Task<LeaveRequestDto> ApproveAsync(int id, CancellationToken ct)
    {
        var request = await LoadDecidableAsync(id, ct);

        request.Status = LeaveStatus.Approved;
        RecordDecision(request);

        // RG-12 : le solde CP est débité à l'approbation, et seulement pour les congés payés.
        if (request.Type == LeaveType.PaidLeave)
        {
            request.Employee!.PaidLeaveBalance -= request.Days;
        }

        await SaveDecisionAsync(ct);
        return request.ToDto();
    }

    public async Task<LeaveRequestDto> RejectAsync(int id, RejectLeaveRequest? rejection, CancellationToken ct)
    {
        var request = await LoadDecidableAsync(id, ct);

        // RG-13 : motif obligatoire d'au moins 10 caractères, espaces de début et de fin exclus (vérifié après le statut).
        var reason = rejection?.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < MinRejectionReasonLength)
        {
            throw LeaveErrors.RejectionReasonRequired();
        }

        // Le solde n'est pas modifié par un refus.
        request.Status = LeaveStatus.Rejected;
        request.RejectionReason = reason;
        RecordDecision(request);

        await SaveDecisionAsync(ct);
        return request.ToDto();
    }

    /// <summary>
    /// Charge une demande à décider, dans l'ordre de FR-024 : existence (404), habilitation (403), statut (409).
    /// </summary>
    private async Task<LeaveRequest> LoadDecidableAsync(int id, CancellationToken ct)
    {
        var request = await db.LeaveRequests
            .Include(r => r.Employee!).ThenInclude(e => e.Team!).ThenInclude(t => t.ParentTeam)
            .SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw LeaveErrors.NotFound(id);

        // RG-10 : seul le valideur décide ; une demande sans valideur (direction générale) n'est décidable par personne.
        if (ApprovalCircuit.ApproverIdOf(request.Employee!) != currentUser.EmployeeId) throw LeaveErrors.ApproverNotAllowed();

        // RG-11 : seule une demande en attente peut être décidée.
        if (request.Status != LeaveStatus.Pending) throw LeaveErrors.AlreadyDecided();

        return request;
    }

    /// <summary>RG-14 : date et auteur de la décision.</summary>
    private void RecordDecision(LeaveRequest request)
    {
        request.DecidedAt = clock.GetUtcNow();
        request.DecidedById = currentUser.EmployeeId;
    }

    /// <summary>
    /// Enregistre la décision et le solde en une seule fois (FR-020). Si le statut a changé entre-temps (décision
    /// concurrente, jeton de concurrence), rien n'est écrit et la décision est refusée (RG-11).
    /// </summary>
    private async Task SaveDecisionAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw LeaveErrors.AlreadyDecided();
        }
    }
}
