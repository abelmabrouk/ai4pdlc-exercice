using Microsoft.EntityFrameworkCore;

namespace Conges.Application.Common;

/// <summary>
/// Vérifie que l'utilisateur courant est identifié et correspond à un collaborateur existant (ADR-005).
/// </summary>
public interface ICurrentUserGuard
{
    /// <exception cref="UnauthenticatedException">En-tête absent, invalide ou collaborateur inconnu.</exception>
    Task EnsureIdentifiedAsync(CancellationToken ct);
}

internal sealed class CurrentUserGuard(ICurrentUser currentUser, IAppDbContext db) : ICurrentUserGuard
{
    public async Task EnsureIdentifiedAsync(CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;

        if (!await db.Employees.AnyAsync(e => e.Id == employeeId, ct))
        {
            throw new UnauthenticatedException();
        }
    }
}
