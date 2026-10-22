using Conges.Domain.Organization;

namespace Conges.Domain.LeaveRequests;

public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public LeaveType Type { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Nombre de jours décomptés pour cette demande.</summary>
    public int Days { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public string? Comment { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public int? DecidedById { get; set; }

    public Employee? Employee { get; set; }
    public Employee? DecidedBy { get; set; }
}
