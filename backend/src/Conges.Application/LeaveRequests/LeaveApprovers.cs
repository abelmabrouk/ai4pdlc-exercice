using System.Linq.Expressions;
using Conges.Domain.LeaveRequests;
using Conges.Domain.Organization;

namespace Conges.Application.LeaveRequests;

/// <summary>
/// Circuit de validation (wiki) : le valideur est le manager de l'équipe du demandeur (N+1) ; pour un manager, celui
/// de l'équipe parente (N+2). Personne ne valide ses propres congés ; la Direction (équipe sans équipe parente) est
/// validée hors application.
/// </summary>
public static class LeaveApprovers
{
    /// <summary>Valideur du collaborateur, ou <c>null</c> s'il n'en a pas dans l'application.</summary>
    /// <remarks>Nécessite <see cref="Employee.Team"/> et <see cref="Team.ParentTeam"/> chargés.</remarks>
    public static int? ApproverOf(Employee employee)
    {
        var team = employee.Team!;
        return employee.Id != team.ManagerId ? team.ManagerId : team.ParentTeam?.ManagerId;
    }

    /// <summary>Même règle que <see cref="ApproverOf"/>, sous forme traduisible en requête.</summary>
    public static Expression<Func<LeaveRequest, bool>> DecidableBy(int userId) => r =>
        (r.Employee!.Id != r.Employee.Team!.ManagerId && r.Employee.Team.ManagerId == userId)
        || (r.Employee!.Id == r.Employee.Team!.ManagerId && r.Employee.Team.ParentTeamId != null
            && r.Employee.Team.ParentTeam!.ManagerId == userId);
}
