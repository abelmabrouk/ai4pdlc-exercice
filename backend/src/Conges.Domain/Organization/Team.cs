namespace Conges.Domain.Organization;

/// <summary>
/// Équipe de l'organigramme. Le manager d'une équipe est lui-même membre de cette équipe.
/// </summary>
public class Team
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? ManagerId { get; set; }
    public int? ParentTeamId { get; set; }

    public Employee? Manager { get; set; }
    public Team? ParentTeam { get; set; }
    public List<Employee> Members { get; set; } = [];
}
