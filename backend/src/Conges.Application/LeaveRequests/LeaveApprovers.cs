using System.Linq.Expressions;
using Conges.Domain.LeaveRequests;
using Conges.Domain.Organization;

namespace Conges.Application.LeaveRequests;

/// <summary>
/// Règle d'habilitation : qui décide les demandes d'un collaborateur.
/// Manager de son équipe ; pour un manager, celui de l'équipe parente ; pour le manager de l'équipe racine,
/// lui-même.
/// </summary>
public static class LeaveApprovers
{
    /// <remarks>Nécessite <see cref="Employee.Team"/> et <see cref="Team.ParentTeam"/> chargés.</remarks>
    public static int? ApproverOf(Employee employee)
    {
        var team = employee.Team!;
        if (employee.Id != team.ManagerId) return team.ManagerId;
        return team.ParentTeam is { } parent ? parent.ManagerId : employee.Id;
    }

    /// <summary>Même règle que <see cref="ApproverOf"/>, sous forme traduisible en requête.</summary>
    public static Expression<Func<LeaveRequest, bool>> DecidableBy(int userId) => r =>
        (r.Employee!.Id != r.Employee.Team!.ManagerId && r.Employee.Team.ManagerId == userId)
        || (r.Employee!.Id == r.Employee.Team!.ManagerId && r.Employee.Team.ParentTeamId != null
            && r.Employee.Team.ParentTeam!.ManagerId == userId)
        || (r.Employee!.Id == r.Employee.Team!.ManagerId && r.Employee.Team.ParentTeamId == null
            && r.EmployeeId == userId);
}
