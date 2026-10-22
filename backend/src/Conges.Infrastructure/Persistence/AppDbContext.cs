using Conges.Application.Common;
using Conges.Domain.LeaveRequests;
using Conges.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Conges.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Team>(team =>
        {
            team.Property(t => t.Name).HasMaxLength(100);
            team.HasOne(t => t.Manager).WithMany().HasForeignKey(t => t.ManagerId).OnDelete(DeleteBehavior.Restrict);
            team.HasOne(t => t.ParentTeam).WithMany().HasForeignKey(t => t.ParentTeamId).OnDelete(DeleteBehavior.Restrict);
            team.HasMany(t => t.Members).WithOne(e => e.Team).HasForeignKey(e => e.TeamId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Employee>(employee =>
        {
            employee.Property(e => e.FirstName).HasMaxLength(100);
            employee.Property(e => e.LastName).HasMaxLength(100);
            employee.Property(e => e.Email).HasMaxLength(200);
            employee.Ignore(e => e.FullName);
        });

        modelBuilder.Entity<LeaveRequest>(request =>
        {
            request.Property(r => r.Type).HasConversion<string>().HasMaxLength(30);
            request.Property(r => r.Status).HasConversion<string>().HasMaxLength(30);
            request.Property(r => r.Comment).HasMaxLength(500);
            request.Property(r => r.RejectionReason).HasMaxLength(500);
            request.HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            request.HasOne(r => r.DecidedBy).WithMany().HasForeignKey(r => r.DecidedById).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
