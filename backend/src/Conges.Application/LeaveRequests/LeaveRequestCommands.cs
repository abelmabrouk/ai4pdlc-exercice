using Conges.Application.Common;
using Conges.Domain.LeaveRequests;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Conges.Application.LeaveRequests;

public sealed record SubmitLeaveRequestDto(LeaveType Type, DateOnly StartDate, DateOnly EndDate, string? Comment);

public sealed record RejectLeaveRequestDto(string? Reason);

public interface ILeaveRequestCommands
{
    Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequestDto input, CancellationToken ct);
    Task<LeaveRequestDto> ApproveAsync(int id, CancellationToken ct);
    Task<LeaveRequestDto> RejectAsync(int id, RejectLeaveRequestDto input, CancellationToken ct);
}

/// <summary>
/// Cas d'usage d'écriture des demandes de congé. Règles de gestion RG-01 à RG-14 du wiki ; chaque violation lève une
/// <see cref="BusinessException"/> porteuse d'un code du référentiel (ADR-003).
/// </summary>
internal sealed class LeaveRequestCommands(
    IAppDbContext db,
    ICurrentUser currentUser,
    IHolidayCalendarService calendar,
    TimeProvider time,
    IValidator<SubmitLeaveRequestDto> submitValidator,
    IValidator<RejectLeaveRequestDto> rejectValidator) : ILeaveRequestCommands
{
    private const int MinimumNoticeWorkingDays = 2;
    private const int MinimumRejectionReasonLength = 10;

    public async Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequestDto input, CancellationToken ct)
    {
        await submitValidator.ValidateAndThrowAsync(input, ct);

        var userId = currentUser.EmployeeId;
        var employee = await db.Employees
            .Include(e => e.Team!).ThenInclude(t => t.ParentTeam)
            .SingleOrDefaultAsync(e => e.Id == userId, ct)
            ?? throw new NotFoundException("Employé", userId);

        // Circuit de validation : la Direction (équipe sans équipe parente) est validée hors application.
        if (LeaveApprovers.ApproverOf(employee) is null)
        {
            throw LeaveErrors.ApproverNotAllowed("Les demandes de la direction générale sont validées hors application.");
        }

        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

        // RG-01
        if (input.EndDate < input.StartDate)
        {
            throw LeaveErrors.InvalidPeriod("La date de fin précède la date de début.");
        }
        if (input.StartDate < today)
        {
            throw LeaveErrors.InvalidPeriod("La date de début est dans le passé.");
        }

        // RG-02 : jours ouvrables
        var days = calendar.CountWorkableDays(input.StartDate, input.EndDate);
        if (days == 0)
        {
            throw LeaveErrors.InvalidPeriod("La période ne contient aucun jour décompté.");
        }

        // RG-03 / RG-04 : jours ouvrés strictement entre la soumission et le premier jour
        if (input.Type == LeaveType.PaidLeave
            && calendar.CountWorkingDays(today.AddDays(1), input.StartDate.AddDays(-1)) < MinimumNoticeWorkingDays)
        {
            throw LeaveErrors.NoticeTooShort();
        }

        // RG-06
        var overlaps = await db.LeaveRequests.AnyAsync(r =>
            r.EmployeeId == userId
            && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved)
            && r.StartDate <= input.EndDate && input.StartDate <= r.EndDate, ct);
        if (overlaps)
        {
            throw LeaveErrors.Overlap();
        }

        // RG-05
        if (input.Type == LeaveType.PaidLeave)
        {
            var pendingDays = await db.LeaveRequests
                .Where(r => r.EmployeeId == userId && r.Type == LeaveType.PaidLeave && r.Status == LeaveStatus.Pending)
                .SumAsync(r => r.Days, ct);
            var available = employee.PaidLeaveBalance - pendingDays;
            if (days > available)
            {
                throw LeaveErrors.InsufficientBalance(days, available);
            }
        }

        // RG-07
        var request = new LeaveRequest
        {
            EmployeeId = userId,
            Type = input.Type,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            Days = days,
            Status = LeaveStatus.Pending,
            Comment = input.Comment,
            SubmittedAt = time.GetUtcNow(),
        };
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync(ct);

        return await ReloadAsync(request.Id, ct);
    }

    public async Task<LeaveRequestDto> ApproveAsync(int id, CancellationToken ct)
    {
        var request = await LoadForDecisionAsync(id, ct);

        // RG-12 : débit à l'approbation, jamais de solde négatif
        if (request.Type == LeaveType.PaidLeave)
        {
            var employee = request.Employee!;
            if (employee.PaidLeaveBalance < request.Days)
            {
                throw LeaveErrors.InsufficientBalance(request.Days, employee.PaidLeaveBalance);
            }
            employee.PaidLeaveBalance -= request.Days;
        }

        request.Status = LeaveStatus.Approved;
        Record(request);
        await SaveDecisionAsync(ct);

        return await ReloadAsync(id, ct);
    }

    public async Task<LeaveRequestDto> RejectAsync(int id, RejectLeaveRequestDto input, CancellationToken ct)
    {
        await rejectValidator.ValidateAndThrowAsync(input, ct);
        var request = await LoadForDecisionAsync(id, ct);

        // RG-13
        var reason = input.Reason?.Trim() ?? "";
        if (reason.Length < MinimumRejectionReasonLength)
        {
            throw LeaveErrors.RejectionReasonRequired();
        }

        request.Status = LeaveStatus.Rejected;
        request.RejectionReason = reason;
        Record(request);
        await SaveDecisionAsync(ct);

        return await ReloadAsync(id, ct);
    }

    /// <summary>RG-14.</summary>
    private void Record(LeaveRequest request)
    {
        request.DecidedAt = time.GetUtcNow();
        request.DecidedById = currentUser.EmployeeId;
    }

    /// <summary>Charge une demande à décider : 401 → 404 → 403 (RG-10) → 409 (RG-11), dans cet ordre.</summary>
    private async Task<LeaveRequest> LoadForDecisionAsync(int id, CancellationToken ct)
    {
        var userId = currentUser.EmployeeId;
        var request = await db.LeaveRequests
            .Include(r => r.Employee!).ThenInclude(e => e.Team!).ThenInclude(t => t.ParentTeam)
            .SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw LeaveErrors.NotFound(id);

        if (LeaveApprovers.ApproverOf(request.Employee!) != userId)
        {
            throw LeaveErrors.ApproverNotAllowed("Vous n'êtes pas le valideur de cette demande.");
        }

        if (request.Status != LeaveStatus.Pending)
        {
            throw LeaveErrors.AlreadyDecided();
        }

        return request;
    }

    /// <summary>RG-11 : une décision concurrente (jeton de concurrence sur le statut) est une demande déjà décidée.</summary>
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

    private async Task<LeaveRequestDto> ReloadAsync(int id, CancellationToken ct)
    {
        var request = await db.LeaveRequests
            .AsNoTracking()
            .Include(r => r.Employee!).ThenInclude(e => e.Team)
            .SingleAsync(r => r.Id == id, ct);
        return request.ToDto();
    }
}
