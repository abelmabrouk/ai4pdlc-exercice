namespace Conges.Domain.Organization;

public class Employee
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public int TeamId { get; set; }

    /// <summary>Solde de congés payés, en jours.</summary>
    public int PaidLeaveBalance { get; set; }

    public Team? Team { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
