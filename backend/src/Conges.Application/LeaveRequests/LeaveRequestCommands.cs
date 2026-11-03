using Conges.Application.Common;
using Conges.Domain.LeaveRequests;
using FluentValidation;
using FluentValidation.Results;
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

internal sealed class LeaveRequestCommands(
    IAppDbContext db,
    ICurrentUser currentUser,
    IHolidayCalendarService calendar,
    TimeProvider time,
    IValidator<SubmitLeaveRequestDto> submitValidator,
    IValidator<RejectLeaveRequestDto> rejectValidator) : ILeaveRequestCommands
{
    public async Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequestDto input, CancellationToken ct)
    {
        await submitValidator.ValidateAndThrowAsync(input, ct);

        var userId = currentUser.EmployeeId;
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == userId, ct)
            ?? throw new NotFoundException("Employé", userId);

        var days = calendar.CountWorkingDays(input.StartDate, input.EndDate);
        if (days == 0)
        {
            throw new ValidationException([
                new ValidationFailure(nameof(input.EndDate), "La période ne contient aucun jour ouvré."),
            ]);
        }

        var overlaps = await db.LeaveRequests.AnyAsync(r =>
            r.EmployeeId == userId
            && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved)
            && r.StartDate <= input.EndDate && input.StartDate <= r.EndDate, ct);
        if (overlaps)
        {
            throw new ConflictException(ConflictCodes.Overlap, "La période chevauche une demande existante.");
        }

        if (input.Type == LeaveType.PaidLeave)
        {
            var pendingDays = await db.LeaveRequests
                .Where(r => r.EmployeeId == userId && r.Type == LeaveType.PaidLeave && r.Status == LeaveStatus.Pending)
                .SumAsync(r => r.Days, ct);
            var available = employee.PaidLeaveBalance - pendingDays;
            if (days > available)
            {
                throw new ConflictException(ConflictCodes.InsufficientBalance,
                    $"Solde insuffisant : {available} jour(s) disponible(s), {days} demandé(s).");
            }
        }

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

        if (request.Type == LeaveType.PaidLeave)
        {
            var employee = request.Employee!;
            if (employee.PaidLeaveBalance < request.Days)
            {
                throw new ConflictException(ConflictCodes.InsufficientBalance,
                    $"Solde insuffisant : {employee.PaidLeaveBalance} jour(s) disponible(s), {request.Days} demandé(s).");
            }
            employee.PaidLeaveBalance -= request.Days;
        }

        request.Status = LeaveStatus.Approved;
        request.DecidedAt = time.GetUtcNow();
        request.DecidedById = currentUser.EmployeeId;
        await SaveDecisionAsync(ct);

        return await ReloadAsync(id, ct);
    }

    public async Task<LeaveRequestDto> RejectAsync(int id, RejectLeaveRequestDto input, CancellationToken ct)
    {
        await rejectValidator.ValidateAndThrowAsync(input, ct);
        var request = await LoadForDecisionAsync(id, ct);

        request.Status = LeaveStatus.Rejected;
        request.RejectionReason = input.Reason!.Trim();
        request.DecidedAt = time.GetUtcNow();
        request.DecidedById = currentUser.EmployeeId;
        await SaveDecisionAsync(ct);

        return await ReloadAsync(id, ct);
    }

    /// <summary>Charge une demande à décider : 401 → 404 → 403 → 409, dans cet ordre.</summary>
    private async Task<LeaveRequest> LoadForDecisionAsync(int id, CancellationToken ct)
    {
        var userId = currentUser.EmployeeId;
        var request = await db.LeaveRequests
            .Include(r => r.Employee!).ThenInclude(e => e.Team!).ThenInclude(t => t.ParentTeam)
            .SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Demande de congé", id);

        if (LeaveApprovers.ApproverOf(request.Employee!) != userId)
        {
            throw new ForbiddenException("Vous n'êtes pas habilité à décider cette demande.");
        }

        if (request.Status != LeaveStatus.Pending)
        {
            throw new ConflictException(ConflictCodes.NotPending, "La demande a déjà été décidée.");
        }

        return request;
    }

    private async Task SaveDecisionAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(ConflictCodes.ConcurrentDecision, "La demande vient d'être décidée par ailleurs.");
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
