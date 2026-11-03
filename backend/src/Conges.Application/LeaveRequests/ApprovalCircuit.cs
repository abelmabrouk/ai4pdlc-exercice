using Conges.Domain.Organization;

namespace Conges.Application.LeaveRequests;

/// <summary>
/// Circuit de validation (RG-10) : une demande est validée par le manager de l'équipe du collaborateur, ou par
/// celui de l'équipe parente si le collaborateur est lui-même le manager de son équipe.
/// </summary>
public static class ApprovalCircuit
{
    /// <summary>Valideur de <paramref name="requester"/>, ou <c>null</c> si personne ne peut décider dans l'application.</summary>
    /// <remarks>Nécessite <see cref="Employee.Team"/> et son <see cref="Team.ParentTeam"/> chargés.</remarks>
    public static int? ApproverOf(Employee requester)
    {
        var team = requester.Team!;
        return team.ManagerId == requester.Id ? team.ParentTeam?.ManagerId : team.ManagerId;
    }
}
