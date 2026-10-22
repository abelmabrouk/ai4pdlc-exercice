using Conges.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Conges.Application.Employees;

public sealed record EmployeeDto(
    int Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    int TeamId,
    string TeamName,
    bool IsManager,
    int PaidLeaveBalance);

public interface IEmployeeQueries
{
    Task<IReadOnlyList<EmployeeDto>> ListAsync(CancellationToken ct);
    Task<EmployeeDto> GetAsync(int id, CancellationToken ct);
}

internal sealed class EmployeeQueries(IAppDbContext db) : IEmployeeQueries
{
    public async Task<IReadOnlyList<EmployeeDto>> ListAsync(CancellationToken ct)
    {
        var employees = await db.Employees
            .AsNoTracking()
            .Include(e => e.Team)
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .ToListAsync(ct);

        var managerIds = await ManagerIdsAsync(ct);
        return employees.Select(e => e.ToDto(managerIds.Contains(e.Id))).ToList();
    }

    public async Task<EmployeeDto> GetAsync(int id, CancellationToken ct)
    {
        var employee = await db.Employees
            .AsNoTracking()
            .Include(e => e.Team)
            .SingleOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Employé", id);

        var managerIds = await ManagerIdsAsync(ct);
        return employee.ToDto(managerIds.Contains(employee.Id));
    }

    private async Task<HashSet<int>> ManagerIdsAsync(CancellationToken ct) =>
        (await db.Teams.Where(t => t.ManagerId != null).Select(t => t.ManagerId!.Value).ToListAsync(ct)).ToHashSet();
}

internal static class EmployeeMapping
{
    public static EmployeeDto ToDto(this Domain.Organization.Employee e, bool isManager) => new(
        e.Id, e.FirstName, e.LastName, e.FullName, e.Email, e.TeamId, e.Team?.Name ?? "", isManager, e.PaidLeaveBalance);
}
