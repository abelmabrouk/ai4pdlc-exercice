using System.Net;
using System.Net.Http.Json;
using Conges.Application.Employees;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;
using Conges.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Conges.Tests;

/// <summary>
/// US2 — Approuver une demande. Date du jour : lundi 2027-02-01. Demandes en attente du seed :
/// #1 Léa (5 j CP, valideur Hugo), #3 Hugo (5 j CP, valideur Claire), #4 Thomas (événement familial, valideur Sophie).
/// Demandes décidées : #5 (approuvée), #6 (refusée).
/// </summary>
public sealed class ApproveLeaveRequestTests
{
    private const int Claire = 1, Hugo = 2, Lea = 3, Sophie = 6, Thomas = 7, Yasmine = 8;

    private static CongesApiFactory Factory() => new() { Today = new DateOnly(2027, 2, 1) };

    private static Task<HttpResponseMessage> Approve(HttpClient client, int id) =>
        client.PostAsync($"/api/leave-requests/{id}/approve", JsonContent.Create(new { }));

    private static async Task<LeaveRequestDto> Ok(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Statut {(int)response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<LeaveRequestDto>(Json.Options))!;
    }

    private static async Task<int> Balance(CongesApiFactory factory, int employeeId) =>
        (await factory.CreateClient().GetFromJsonAsync<EmployeeDto>($"/api/employees/{employeeId}", Json.Options))!.PaidLeaveBalance;

    private static async Task<LeaveStatus> Status(CongesApiFactory factory, int id) =>
        (await factory.CreateClient().GetFromJsonAsync<LeaveRequestDto>($"/api/leave-requests/{id}", Json.Options))!.Status;

    [Fact]
    public async Task RG10_RG12_RG14_approving_paid_leave_records_the_decision_and_debits_the_balance()
    {
        using var factory = Factory();

        var approved = await Ok(await Approve(factory.CreateClientAs(Hugo), 1));

        Assert.Equal(LeaveStatus.Approved, approved.Status);
        Assert.Equal(Hugo, approved.DecidedById);
        Assert.Equal(new DateTimeOffset(2027, 2, 1, 9, 0, 0, TimeSpan.Zero), approved.DecidedAt);
        Assert.Equal(LeaveStatus.Approved, await Status(factory, 1));
        Assert.Equal(25, await Balance(factory, Lea));
    }

    [Fact]
    public async Task RG12_approving_a_family_event_does_not_debit_the_balance()
    {
        using var factory = Factory();

        var approved = await Ok(await Approve(factory.CreateClientAs(Sophie), 4));

        Assert.Equal(LeaveStatus.Approved, approved.Status);
        Assert.Equal(30, await Balance(factory, Thomas));
    }

    [Fact]
    public async Task RG10_a_manager_request_is_decided_by_the_manager_of_the_parent_team()
    {
        using var factory = Factory();

        await Ok(await Approve(factory.CreateClientAs(Claire), 3));

        Assert.Equal(25, await Balance(factory, Hugo));
    }

    [Fact]
    public async Task RG10_a_manager_cannot_approve_their_own_request()
    {
        using var factory = Factory();

        await ProblemAssert.HasCode(await Approve(factory.CreateClientAs(Hugo), 3), HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");

        Assert.Equal(LeaveStatus.Pending, await Status(factory, 3));
        Assert.Equal(30, await Balance(factory, Hugo));
    }

    [Theory]
    [InlineData(Sophie)] // manager d'une autre équipe
    [InlineData(Claire)] // N+2 : pas de délégation
    public async Task RG10_only_the_approver_can_approve(int someoneElse)
    {
        using var factory = Factory();

        await ProblemAssert.HasCode(await Approve(factory.CreateClientAs(someoneElse), 1), HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");

        Assert.Equal(LeaveStatus.Pending, await Status(factory, 1));
        Assert.Equal(30, await Balance(factory, Lea));
    }

    [Fact]
    public async Task RG10_requests_of_the_general_management_cannot_be_decided_in_the_application()
    {
        using var factory = Factory();
        var claire = factory.CreateClientAs(Claire);
        var submitted = await claire.PostAsJsonAsync("/api/leave-requests",
            new { type = "FamilyEvent", startDate = "2027-03-08", endDate = "2027-03-08" });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var request = (await submitted.Content.ReadFromJsonAsync<LeaveRequestDto>(Json.Options))!;

        await ProblemAssert.HasCode(await Approve(claire, request.Id), HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
    }

    [Theory]
    [InlineData(5)] // déjà approuvée
    [InlineData(6)] // déjà refusée
    public async Task RG11_a_decided_request_cannot_be_approved(int decidedId)
    {
        using var factory = Factory();

        await ProblemAssert.HasCode(await Approve(factory.CreateClientAs(Sophie), decidedId), HttpStatusCode.Conflict, "LEAVE_ALREADY_DECIDED");

        Assert.Equal(25, await Balance(factory, Yasmine));
    }

    [Fact]
    public async Task RG11_approving_twice_does_not_debit_the_balance_twice()
    {
        using var factory = Factory();
        var hugo = factory.CreateClientAs(Hugo);
        await Ok(await Approve(hugo, 1));

        await ProblemAssert.HasCode(await Approve(hugo, 1), HttpStatusCode.Conflict, "LEAVE_ALREADY_DECIDED");

        Assert.Equal(25, await Balance(factory, Lea));
    }

    [Fact]
    public async Task Approving_an_unknown_request_returns_leave_not_found()
    {
        using var factory = Factory();

        await ProblemAssert.HasCode(await Approve(factory.CreateClientAs(Hugo), 9999), HttpStatusCode.NotFound, "LEAVE_NOT_FOUND");
    }

    [Fact]
    public async Task Approving_requires_an_identified_user()
    {
        using var factory = Factory();

        // L'identification est contrôlée avant toute chose, même pour une demande inexistante.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Approve(factory.CreateClient(), 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Approve(factory.CreateClient(), 9999)).StatusCode);
    }

    [Fact]
    public async Task RG11_a_decision_based_on_a_stale_read_is_rejected_by_the_database()
    {
        // Décisions simultanées : une décision lue avant qu'une autre soit enregistrée ne doit pas l'écraser.
        using var factory = Factory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stale = await db.LeaveRequests.SingleAsync(r => r.Id == 1);

        await Ok(await Approve(factory.CreateClientAs(Hugo), 1));
        stale.Status = LeaveStatus.Rejected;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
    }
}
