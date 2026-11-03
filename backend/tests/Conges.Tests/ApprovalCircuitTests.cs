using Conges.Application.LeaveRequests;
using Conges.Domain.Organization;

namespace Conges.Tests;

/// <summary>RG-10 : algorithme du circuit de validation (wiki « Circuit de validation »).</summary>
public sealed class ApprovalCircuitTests
{
    private static Employee Member(int id, Team team)
    {
        var employee = new Employee { Id = id, FirstName = $"P{id}", LastName = "Test", Email = $"p{id}@test", TeamId = team.Id, Team = team };
        team.Members.Add(employee);
        return employee;
    }

    [Fact]
    public void RG10_a_team_member_is_validated_by_the_team_manager()
    {
        var produit = new Team { Id = 2, Name = "Produit", ManagerId = 2 };

        Assert.Equal(2, ApprovalCircuit.ApproverOf(Member(3, produit)));
    }

    [Fact]
    public void RG10_a_manager_is_validated_by_the_manager_of_the_parent_team()
    {
        var direction = new Team { Id = 1, Name = "Direction", ManagerId = 1 };
        var produit = new Team { Id = 2, Name = "Produit", ManagerId = 2, ParentTeamId = 1, ParentTeam = direction };

        Assert.Equal(1, ApprovalCircuit.ApproverOf(Member(2, produit)));
    }

    [Fact]
    public void RG10_the_manager_of_a_team_without_parent_has_no_approver()
    {
        var direction = new Team { Id = 1, Name = "Direction", ManagerId = 1 };

        Assert.Null(ApprovalCircuit.ApproverOf(Member(1, direction)));
    }

    [Fact]
    public void RG10_a_member_of_a_team_without_manager_has_no_approver()
    {
        var direction = new Team { Id = 1, Name = "Direction", ManagerId = 1 };
        var orpheline = new Team { Id = 4, Name = "Sans manager", ParentTeamId = 1, ParentTeam = direction };

        Assert.Null(ApprovalCircuit.ApproverOf(Member(10, orpheline)));
    }

    [Fact]
    public void RG10_a_manager_whose_parent_team_has_no_manager_has_no_approver()
    {
        var parent = new Team { Id = 1, Name = "Parent sans manager" };
        var produit = new Team { Id = 2, Name = "Produit", ManagerId = 2, ParentTeamId = 1, ParentTeam = parent };

        Assert.Null(ApprovalCircuit.ApproverOf(Member(2, produit)));
    }
}
