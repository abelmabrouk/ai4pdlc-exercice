using Conges.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Conges.Application.Teams;

public sealed record TeamDto(int Id, string Name, int? ManagerId, string? ManagerName, int? ParentTeamId);

public interface ITeamQueries
{
    Task<IReadOnlyList<TeamDto>> ListAsync(CancellationToken ct);
}

internal sealed class TeamQueries(IAppDbContext db) : ITeamQueries
{
    public async Task<IReadOnlyList<TeamDto>> ListAsync(CancellationToken ct) =>
        await db.Teams
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new TeamDto(
                t.Id,
                t.Name,
                t.ManagerId,
                t.Manager == null ? null : t.Manager.FirstName + " " + t.Manager.LastName,
                t.ParentTeamId))
            .ToListAsync(ct);
}
