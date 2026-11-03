using Conges.Application.LeaveRequests;
using Conges.Domain.Organization;

namespace Conges.Tests;

/// <summary>Circuit de validation (wiki « Circuit de validation », RG-10).</summary>
public sealed class ApprovalCircuitTests
{
    private readonly Team _direction = new() { Id = 1, Name = "Direction", ManagerId = 1 };
    private readonly Team _produit;
    private readonly Team _support;

    public ApprovalCircuitTests()
    {
        _produit = new Team { Id = 2, Name = "Produit", ManagerId = 2, ParentTeamId = 1, ParentTeam = _direction };
        _support = new Team { Id = 3, Name = "Support", ManagerId = 6, ParentTeamId = 1, ParentTeam = _direction };
    }

    private static Employee Member(int id, Team team) => new()
    {
        Id = id, FirstName = "Prénom", LastName = $"Nom{id}", Email = $"e{id}@conges.example", TeamId = team.Id, Team = team,
    };

    [Fact]
    public void RG10_member_is_validated_by_the_manager_of_their_team() =>
        Assert.Equal(2, ApprovalCircuit.ApproverIdOf(Member(3, _produit)));

    [Fact]
    public void RG10_member_of_support_is_validated_by_sophie() =>
        Assert.Equal(6, ApprovalCircuit.ApproverIdOf(Member(7, _support)));

    [Fact]
    public void RG10_manager_is_validated_by_the_manager_of_the_parent_team() =>
        Assert.Equal(1, ApprovalCircuit.ApproverIdOf(Member(2, _produit)));

    [Fact]
    public void RG10_support_manager_is_validated_by_claire() =>
        Assert.Equal(1, ApprovalCircuit.ApproverIdOf(Member(6, _support)));

    [Fact]
    public void RG10_manager_of_a_team_without_parent_has_no_approver() =>
        Assert.Null(ApprovalCircuit.ApproverIdOf(Member(1, _direction)));

    [Fact]
    public void RG10_non_manager_member_of_direction_is_validated_by_claire() =>
        Assert.Equal(1, ApprovalCircuit.ApproverIdOf(Member(10, _direction)));

    [Fact]
    public void Team_must_be_loaded()
    {
        var employee = Member(3, _produit);
        employee.Team = null;

        Assert.Throws<InvalidOperationException>(() => ApprovalCircuit.ApproverIdOf(employee));
    }

    [Fact]
    public void Parent_team_must_be_loaded_for_a_manager()
    {
        var produitWithoutParent = new Team { Id = 2, Name = "Produit", ManagerId = 2, ParentTeamId = 1 };

        Assert.Throws<InvalidOperationException>(() => ApprovalCircuit.ApproverIdOf(Member(2, produitWithoutParent)));
    }
}
