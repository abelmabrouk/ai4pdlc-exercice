using System.Net;
using System.Net.Http.Json;
using Conges.Application.Employees;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;
using Conges.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static Conges.Tests.Problems;

namespace Conges.Tests;

/// <summary>Approbation (US3) et refus (US4) d'une demande, RG-10 à RG-14. Chaque test part d'une base neuve.</summary>
public sealed class DecideLeaveRequestTests
{
    private const int Claire = 1, Hugo = 2, Lea = 3, Karim = 4, Ines = 5, Sophie = 6, Thomas = 7;
    private const HttpStatusCode Unprocessable = (HttpStatusCode)422;

    // Demandes seedées : 1 (Lea, 5 j), 2 (Karim), 3 (Hugo), 4 (Thomas, FamilyEvent), 5 Approved, 6 Rejected, 7 (Ines, 2 j).
    private static Task<HttpResponseMessage> Approve(HttpClient client, int id) =>
        client.PostAsJsonAsync($"/api/leave-requests/{id}/approve", new { });

    private static Task<HttpResponseMessage> Reject(HttpClient client, int id, object body) =>
        client.PostAsJsonAsync($"/api/leave-requests/{id}/reject", body);

    private static async Task<int> BalanceOf(HttpClient client, int employeeId) =>
        (await client.GetFromJsonAsync<EmployeeDto>($"/api/employees/{employeeId}", Json.Options))!.PaidLeaveBalance;

    private static async Task<LeaveRequestDto> Read(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<LeaveRequestDto>(Json.Options))!;

    [Fact] // RG-12, RG-14
    public async Task Approving_paid_leave_debits_the_balance()
    {
        using var factory = new CongesApiFactory();
        var hugo = factory.CreateClientAs(Hugo);

        var response = await Approve(hugo, 1);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var approved = await Read(response);
        Assert.Equal(LeaveStatus.Approved, approved.Status);
        Assert.Equal(Hugo, approved.DecidedById);
        Assert.NotNull(approved.DecidedAt);
        Assert.Equal(25, await BalanceOf(hugo, Lea));
    }

    [Fact] // RG-12
    public async Task Approving_a_family_event_keeps_the_balance()
    {
        using var factory = new CongesApiFactory();
        var sophie = factory.CreateClientAs(Sophie);

        var response = await Approve(sophie, 4);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(30, await BalanceOf(sophie, Thomas));
    }

    [Fact] // RG-10
    public async Task A_manager_of_another_team_cannot_approve()
    {
        using var factory = new CongesApiFactory();
        var sophie = factory.CreateClientAs(Sophie);

        var response = await Approve(sophie, 2);

        await AssertProblem(response, HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
        var request = await sophie.GetFromJsonAsync<LeaveRequestDto>("/api/leave-requests/2", Json.Options);
        Assert.Equal(LeaveStatus.Pending, request!.Status);
    }

    [Fact] // RG-10 : personne ne valide ses propres congés
    public async Task A_manager_cannot_approve_their_own_request()
    {
        using var factory = new CongesApiFactory();

        var response = await Approve(factory.CreateClientAs(Hugo), 3);

        await AssertProblem(response, HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
    }

    [Fact] // RG-10 : la demande d'un manager remonte au N+2
    public async Task The_parent_team_manager_approves_a_manager_request()
    {
        using var factory = new CongesApiFactory();

        var response = await Approve(factory.CreateClientAs(Claire), 3);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact] // RG-10 : pas de délégation, le N+2 ne décide pas à la place du N+1
    public async Task The_parent_team_manager_cannot_decide_for_a_team_manager()
    {
        using var factory = new CongesApiFactory();

        var response = await Approve(factory.CreateClientAs(Claire), 1);

        await AssertProblem(response, HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
    }

    [Fact] // RG-11
    public async Task Approving_twice_is_a_conflict_and_debits_once()
    {
        using var factory = new CongesApiFactory();
        var hugo = factory.CreateClientAs(Hugo);
        await Approve(hugo, 1);

        var response = await Approve(hugo, 1);

        await AssertProblem(response, HttpStatusCode.Conflict, "LEAVE_ALREADY_DECIDED");
        Assert.Equal(25, await BalanceOf(hugo, Lea));
    }

    [Fact] // RG-11
    public async Task An_already_decided_request_cannot_be_approved()
    {
        using var factory = new CongesApiFactory();

        var response = await Approve(factory.CreateClientAs(Sophie), 5);

        await AssertProblem(response, HttpStatusCode.Conflict, "LEAVE_ALREADY_DECIDED");
    }

    [Fact] // Codes d'erreur : LEAVE_NOT_FOUND
    public async Task Approving_an_unknown_request_returns_leave_not_found()
    {
        using var factory = new CongesApiFactory();

        var response = await Approve(factory.CreateClientAs(Hugo), 9999);

        await AssertProblem(response, HttpStatusCode.NotFound, "LEAVE_NOT_FOUND");
    }

    [Fact] // RG-12 : le solde n'est jamais négatif
    public async Task Approval_is_refused_when_the_balance_became_insufficient()
    {
        using var factory = new CongesApiFactory();
        var hugo = factory.CreateClientAs(Hugo);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Employees.Single(e => e.Id == Ines).PaidLeaveBalance = 1;
            db.SaveChanges();
        }

        var response = await Approve(hugo, 7);

        await AssertProblem(response, Unprocessable, "LEAVE_INSUFFICIENT_BALANCE");
        Assert.Equal(1, await BalanceOf(hugo, Ines));
    }

    [Fact] // ADR-005
    public async Task Approving_requires_the_user_header()
    {
        using var factory = new CongesApiFactory();

        var response = await Approve(factory.CreateClient(), 1);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact] // RG-13, RG-14
    public async Task Rejecting_records_the_reason_and_keeps_the_balance()
    {
        using var factory = new CongesApiFactory();
        var hugo = factory.CreateClientAs(Hugo);

        var response = await Reject(hugo, 2, new { reason = "Période de forte activité" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rejected = await Read(response);
        Assert.Equal(LeaveStatus.Rejected, rejected.Status);
        Assert.Equal("Période de forte activité", rejected.RejectionReason);
        Assert.Equal(Hugo, rejected.DecidedById);
        Assert.NotNull(rejected.DecidedAt);
        Assert.Equal(30, await BalanceOf(hugo, Karim));
    }

    public static TheoryData<object> InvalidReasons => new()
    {
        new { },                          // absent
        new { reason = "" },              // vide
        new { reason = "Trop tôt" },      // 8 caractères
        new { reason = "   court    " },  // 5 caractères une fois les espaces retirés
    };

    [Theory] // RG-13
    [MemberData(nameof(InvalidReasons))]
    public async Task Rejecting_requires_a_reason_of_at_least_ten_characters(object body)
    {
        using var factory = new CongesApiFactory();

        var response = await Reject(factory.CreateClientAs(Hugo), 2, body);

        await AssertProblem(response, Unprocessable, "LEAVE_REJECTION_REASON_REQUIRED");
    }

    [Fact] // RG-13 : exactement 10 caractères après suppression des espaces
    public async Task A_ten_character_reason_is_accepted()
    {
        using var factory = new CongesApiFactory();

        var response = await Reject(factory.CreateClientAs(Hugo), 2, new { reason = "  0123456789  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("0123456789", (await Read(response)).RejectionReason);
    }

    [Fact] // RG-10
    public async Task A_manager_of_another_team_cannot_reject()
    {
        using var factory = new CongesApiFactory();

        var response = await Reject(factory.CreateClientAs(Sophie), 2, new { reason = "Période de forte activité" });

        await AssertProblem(response, HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
    }

    [Fact] // RG-11
    public async Task An_already_decided_request_cannot_be_rejected()
    {
        using var factory = new CongesApiFactory();

        var response = await Reject(factory.CreateClientAs(Sophie), 6, new { reason = "Période de forte activité" });

        await AssertProblem(response, HttpStatusCode.Conflict, "LEAVE_ALREADY_DECIDED");
    }

    [Fact] // Codes d'erreur : LEAVE_NOT_FOUND
    public async Task Rejecting_an_unknown_request_returns_leave_not_found()
    {
        using var factory = new CongesApiFactory();

        var response = await Reject(factory.CreateClientAs(Hugo), 9999, new { reason = "Période de forte activité" });

        await AssertProblem(response, HttpStatusCode.NotFound, "LEAVE_NOT_FOUND");
    }
}
