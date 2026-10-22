using Conges.Domain.LeaveRequests;
using Conges.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Conges.Infrastructure.Persistence;

/// <summary>
/// Recrée la base SQLite et charge le jeu de données de démonstration à chaque démarrage
/// (pas de migrations pendant l'atelier : le schéma suit le modèle EF Core).
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        if (config.GetValue("Database:ResetOnStartup", true))
        {
            await db.Database.EnsureDeletedAsync();
        }

        if (await db.Database.EnsureCreatedAsync())
        {
            Seed(db, DateOnly.FromDateTime(clock.GetLocalNow().Date));
            await db.SaveChangesAsync();
        }
    }

    private static void Seed(AppDbContext db, DateOnly today)
    {
        Team direction = new() { Id = 1, Name = "Direction" };
        Team produit = new() { Id = 2, Name = "Produit", ParentTeamId = 1 };
        Team support = new() { Id = 3, Name = "Support", ParentTeamId = 1 };
        db.Teams.AddRange(direction, produit, support);

        Employee E(int id, string first, string last, int teamId, int balance) => new()
        {
            Id = id,
            FirstName = first,
            LastName = last,
            Email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@conges.example",
            TeamId = teamId,
            PaidLeaveBalance = balance,
        };

        db.Employees.AddRange(
            E(1, "Claire", "Martin", 1, 30),
            E(2, "Hugo", "Bernard", 2, 30),
            E(3, "Lea", "Petit", 2, 30),
            E(4, "Karim", "Benali", 2, 30),
            E(5, "Ines", "Moreau", 2, 3),
            E(6, "Sophie", "Laurent", 3, 30),
            E(7, "Thomas", "Roux", 3, 30),
            E(8, "Yasmine", "Haddad", 3, 25),
            E(9, "Nicolas", "Fournier", 3, 30));
        db.SaveChanges();

        direction.ManagerId = 1;
        produit.ManagerId = 2;
        support.ManagerId = 6;

        // Les demandes de démonstration portent sur des semaines du lundi au vendredi.
        DateOnly Monday(int weeksFromNow)
        {
            var d = today.AddDays(7 * weeksFromNow);
            return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
        }

        var submittedAt = new DateTimeOffset(today.AddDays(-3).ToDateTime(new TimeOnly(9, 30)), TimeSpan.Zero);

        LeaveRequest R(int id, int employeeId, LeaveType type, DateOnly start, int days, LeaveStatus status, string? comment = null) => new()
        {
            Id = id,
            EmployeeId = employeeId,
            Type = type,
            StartDate = start,
            EndDate = start.AddDays(days - 1),
            Days = days,
            Status = status,
            Comment = comment,
            SubmittedAt = submittedAt,
        };

        db.LeaveRequests.AddRange(
            R(1, 3, LeaveType.PaidLeave, Monday(4), 5, LeaveStatus.Pending, "Vacances en famille"),
            R(2, 4, LeaveType.PaidLeave, Monday(6), 3, LeaveStatus.Pending),
            R(3, 2, LeaveType.PaidLeave, Monday(5), 5, LeaveStatus.Pending, "Séminaire annuel décalé"),
            R(4, 7, LeaveType.FamilyEvent, Monday(2).AddDays(3), 2, LeaveStatus.Pending, "Mariage de ma sœur"),
            R(5, 8, LeaveType.PaidLeave, Monday(-5), 5, LeaveStatus.Approved),
            R(6, 9, LeaveType.PaidLeave, Monday(-3), 2, LeaveStatus.Rejected),
            R(7, 5, LeaveType.PaidLeave, Monday(8), 2, LeaveStatus.Pending),
            R(8, 8, LeaveType.PaidLeave, Monday(7), 4, LeaveStatus.Pending, "Pont de printemps"));

        foreach (var decided in db.LeaveRequests.Local.Where(r => r.Status != LeaveStatus.Pending))
        {
            decided.DecidedAt = submittedAt.AddDays(1);
            decided.DecidedById = 6;
            if (decided.Status == LeaveStatus.Rejected) decided.RejectionReason = "Période de clôture trimestrielle";
        }
    }
}
