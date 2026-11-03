using Conges.Api.Http;
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
    [RequireIdentifiedUser]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IReadOnlyList<LeaveRequestDto>> Pending(CancellationToken ct) => leaveRequests.ListPendingToDecideAsync(ct);

    /// <summary>Soumet une demande de congé pour l'utilisateur courant (en-tête X-User-Id).</summary>
    [HttpPost]
    [RequireIdentifiedUser]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LeaveRequestDto>> Submit([FromBody] SubmitLeaveRequest request, CancellationToken ct)
    {
        var created = await commands.SubmitAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Approuve une demande en attente (réservé à son valideur).</summary>
    [HttpPost("{id:int}/approve")]
    [RequireIdentifiedUser]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<LeaveRequestDto> Approve(int id, CancellationToken ct) => commands.ApproveAsync(id, ct);

    /// <summary>Refuse une demande en attente avec un motif (réservé à son valideur).</summary>
    /// <remarks>Le corps est facultatif : un motif absent est une erreur métier (422), pas une erreur de forme.</remarks>
    [HttpPost("{id:int}/reject")]
    [RequireIdentifiedUser]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<LeaveRequestDto> Reject(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RejectLeaveRequest? request,
        CancellationToken ct) =>
        commands.RejectAsync(id, request, ct);
}
