using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;
using Microsoft.AspNetCore.Mvc;

namespace Conges.Api.Controllers;

[ApiController]
[Route("api/leave-requests")]
[Produces("application/json")]
public sealed class LeaveRequestsController(ILeaveRequestQueries leaveRequests) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IReadOnlyList<LeaveRequestDto>> List(
        [FromQuery] int? employeeId,
        [FromQuery] int? teamId,
        [FromQuery] LeaveStatus? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        leaveRequests.ListAsync(new LeaveRequestFilter(employeeId, teamId, status, from, to), ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<LeaveRequestDto> Get(int id, CancellationToken ct) => leaveRequests.GetAsync(id, ct);
}
