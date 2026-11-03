using Conges.Domain.LeaveRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Conges.Tests;

/// <summary>
/// Simule une décision concurrente : juste avant le premier enregistrement qui modifie la demande ciblée, une autre
/// « décision » la passe en <c>Rejected</c> directement en base.
/// </summary>
public sealed class ConcurrentDecisionInterceptor(int leaveRequestId) : SaveChangesInterceptor
{
    private bool _triggered;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context!;
        var modifiesTarget = context.ChangeTracker.Entries<LeaveRequest>()
            .Any(e => e.State == EntityState.Modified && e.Entity.Id == leaveRequestId);

        if (!_triggered && modifiesTarget)
        {
            _triggered = true;
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE LeaveRequests SET Status = 'Rejected' WHERE Id = {0}", [leaveRequestId], cancellationToken);
        }

        return result;
    }
}
