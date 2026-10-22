using Conges.Domain.LeaveRequests;
using Conges.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Conges.Application.Common;

public interface IAppDbContext
{
    DbSet<Team> Teams { get; }
    DbSet<Employee> Employees { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Utilisateur à l'origine de la requête HTTP (en-tête <c>X-User-Id</c>).
/// </summary>
public interface ICurrentUser
{
    /// <exception cref="UnauthenticatedException">Si l'en-tête est absent ou invalide.</exception>
    int EmployeeId { get; }
}
