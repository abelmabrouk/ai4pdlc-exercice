using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Conges.Api.Controllers;

[ApiController]
[Route("api/leave-requests")]
[Produces("application/json")]
public sealed class LeaveRequestsController(ILeaveRequestQueries leaveRequests, ILeaveRequestCommands commands) : ControllerBase
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

    /// <summary>Demandes en attente que l'utilisateur courant est habilité à décider.</summary>
    [HttpGet("pending")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IReadOnlyList<LeaveRequestDto>> Pending(CancellationToken ct) => leaveRequests.ListPendingToDecideAsync(ct);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LeaveRequestDto>> Submit(SubmitLeaveRequest request, CancellationToken ct)
    {
        var created = await commands.SubmitAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<LeaveRequestDto> Approve(int id, CancellationToken ct) => commands.ApproveAsync(id, ct);

    /// <remarks>Un corps vide est accepté : l'absence de motif relève de RG-13 (422), pas d'une erreur de forme.</remarks>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<LeaveRequestDto> Reject(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RejectLeaveRequest? body,
        CancellationToken ct) =>
        commands.RejectAsync(id, body?.Reason, ct);
}
