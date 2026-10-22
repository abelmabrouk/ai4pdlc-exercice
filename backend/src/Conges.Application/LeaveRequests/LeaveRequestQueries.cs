using Conges.Application.Common;
using Conges.Domain.LeaveRequests;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Conges.Application.LeaveRequests;

public sealed record LeaveRequestDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    int TeamId,
    string TeamName,
    LeaveType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days,
    LeaveStatus Status,
    string? Comment,
    string? RejectionReason,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt,
    int? DecidedById);

public sealed record LeaveRequestFilter(int? EmployeeId, int? TeamId, LeaveStatus? Status, DateOnly? From, DateOnly? To);

public sealed class LeaveRequestFilterValidator : AbstractValidator<LeaveRequestFilter>
{
    public LeaveRequestFilterValidator()
    {
        RuleFor(f => f.To)
            .GreaterThanOrEqualTo(f => f.From)
            .When(f => f.From is not null && f.To is not null)
            .WithMessage("La date de fin du filtre doit être postérieure ou égale à la date de début.");
    }
}

public interface ILeaveRequestQueries
{
    Task<IReadOnlyList<LeaveRequestDto>> ListAsync(LeaveRequestFilter filter, CancellationToken ct);
    Task<LeaveRequestDto> GetAsync(int id, CancellationToken ct);
}

internal sealed class LeaveRequestQueries(IAppDbContext db, IValidator<LeaveRequestFilter> filterValidator)
    : ILeaveRequestQueries
{
    public async Task<IReadOnlyList<LeaveRequestDto>> ListAsync(LeaveRequestFilter filter, CancellationToken ct)
    {
        await filterValidator.ValidateAndThrowAsync(filter, ct);

        var query = db.LeaveRequests.AsNoTracking().Include(r => r.Employee!).ThenInclude(e => e.Team).AsQueryable();

        if (filter.EmployeeId is { } employeeId) query = query.Where(r => r.EmployeeId == employeeId);
        if (filter.TeamId is { } teamId) query = query.Where(r => r.Employee!.TeamId == teamId);
        if (filter.Status is { } status) query = query.Where(r => r.Status == status);
        if (filter.From is { } from) query = query.Where(r => r.EndDate >= from);
        if (filter.To is { } to) query = query.Where(r => r.StartDate <= to);

        var requests = await query.OrderBy(r => r.StartDate).ThenBy(r => r.Id).ToListAsync(ct);
        return requests.Select(r => r.ToDto()).ToList();
    }

    public async Task<LeaveRequestDto> GetAsync(int id, CancellationToken ct)
    {
        var request = await db.LeaveRequests
            .AsNoTracking()
            .Include(r => r.Employee!).ThenInclude(e => e.Team)
            .SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Demande de congé", id);

        return request.ToDto();
    }
}

public static class LeaveRequestMapping
{
    /// <remarks>Nécessite <see cref="LeaveRequest.Employee"/> et son équipe chargés.</remarks>
    public static LeaveRequestDto ToDto(this LeaveRequest r) => new(
        r.Id,
        r.EmployeeId,
        r.Employee?.FullName ?? "",
        r.Employee?.TeamId ?? 0,
        r.Employee?.Team?.Name ?? "",
        r.Type,
        r.StartDate,
        r.EndDate,
        r.Days,
        r.Status,
        r.Comment,
        r.RejectionReason,
        r.SubmittedAt,
        r.DecidedAt,
        r.DecidedById);
}
