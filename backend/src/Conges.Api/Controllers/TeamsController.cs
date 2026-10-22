using Conges.Application.Teams;
using Microsoft.AspNetCore.Mvc;

namespace Conges.Api.Controllers;

[ApiController]
[Route("api/teams")]
[Produces("application/json")]
public sealed class TeamsController(ITeamQueries teams) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TeamDto>> List(CancellationToken ct) => teams.ListAsync(ct);
}
