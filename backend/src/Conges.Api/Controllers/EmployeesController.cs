using Conges.Application.Common;
using Conges.Application.Employees;
using Microsoft.AspNetCore.Mvc;

namespace Conges.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class EmployeesController(IEmployeeQueries employees) : ControllerBase
{
    [HttpGet("employees")]
    public Task<IReadOnlyList<EmployeeDto>> List(CancellationToken ct) => employees.ListAsync(ct);

    [HttpGet("employees/{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<EmployeeDto> Get(int id, CancellationToken ct) => employees.GetAsync(id, ct);

    /// <summary>Employé courant (en-tête X-User-Id).</summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<EmployeeDto> Me([FromServices] ICurrentUser currentUser, CancellationToken ct) =>
        employees.GetAsync(currentUser.EmployeeId, ct);
}
