using Conges.Domain.Organization;

namespace Conges.Application.LeaveRequests;

/// <summary>
/// Circuit de validation (wiki « Circuit de validation ») : qui décide la demande d'un collaborateur.
/// </summary>
public static class ApprovalCircuit
{
    /// <summary>
    /// Valideur des demandes de <paramref name="requester"/> : le manager de son équipe, ou, si le demandeur est
    /// lui-même ce manager, le manager de l'équipe parente. <c>null</c> pour une équipe sans équipe parente
    /// (direction générale, demandes validées hors application).
    /// </summary>
    /// <remarks>Nécessite <see cref="Employee.Team"/> et <see cref="Team.ParentTeam"/> chargés.</remarks>
    public static int? ApproverIdOf(Employee requester)
    {
        var team = requester.Team
            ?? throw new InvalidOperationException("L'équipe du demandeur doit être chargée.");

        if (team.ManagerId != requester.Id)
        {
            return team.ManagerId;
        }

        if (team.ParentTeamId is null)
        {
            return null;
        }

        var parentTeam = team.ParentTeam
            ?? throw new InvalidOperationException("L'équipe parente du demandeur doit être chargée.");

        return parentTeam.ManagerId;
    }
}
