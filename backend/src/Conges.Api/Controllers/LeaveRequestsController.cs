using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;
using Microsoft.AspNetCore.Mvc;

namespace Conges.Api.Controllers;

[ApiController]
[Route("api/leave-requests")]
[Produces("application/json")]
public sealed class LeaveRequestsController(ILeaveRequestQueries leaveRequests, ILeaveRequestCommands commands)
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaveRequestDto>> Submit(SubmitLeaveRequestDto input, CancellationToken ct)
    {
        var created = await commands.SubmitAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

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

    [HttpGet("pending")]
    public Task<IReadOnlyList<LeaveRequestDto>> Pending(CancellationToken ct) => leaveRequests.PendingAsync(ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<LeaveRequestDto> Get(int id, CancellationToken ct) => leaveRequests.GetAsync(id, ct);

    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<LeaveRequestDto> Approve(int id, CancellationToken ct) => commands.ApproveAsync(id, ct);

    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<LeaveRequestDto> Reject(int id, RejectLeaveRequestDto input, CancellationToken ct) =>
        commands.RejectAsync(id, input, ct);
}
