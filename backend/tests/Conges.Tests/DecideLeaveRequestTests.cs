using System.Net;
using System.Net.Http.Json;
using Conges.Application.Employees;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;
using Conges.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Conges.Tests;

/// <summary>Approbation (US3) et refus (US4) d'une demande. Chaque test part d'une base neuve.</summary>
public sealed class DecideLeaveRequestTests
{
    private const int Claire = 1, Hugo = 2, Lea = 3, Karim = 4, Ines = 5, Sophie = 6, Thomas = 7;

    // Demandes seedées : 1 (Lea, 5 j), 2 (Karim), 3 (Hugo), 4 (Thomas, FamilyEvent), 5 Approved, 6 Rejected, 7 (Ines, 2 j).
    private static Task<HttpResponseMessage> Approve(HttpClient client, int id) =>
        client.PostAsJsonAsync($"/api/leave-requests/{id}/approve", new { });

    private static async Task<int> BalanceOf(HttpClient client, int employeeId) =>
        (await client.GetFromJsonAsync<EmployeeDto>($"/api/employees/{employeeId}", Json.Options))!.PaidLeaveBalance;

    private static async Task<LeaveRequestDto> Read(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<LeaveRequestDto>(Json.Options))!;

    [Fact]
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

    [Fact]
    public async Task Approving_a_family_event_keeps_the_balance()
    {
        using var factory = new CongesApiFactory();
        var sophie = factory.CreateClientAs(Sophie);

        var response = await Approve(sophie, 4);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(30, await BalanceOf(sophie, Thomas));
    }

    [Fact]
    public async Task A_manager_of_another_team_cannot_approve()
    {
        using var factory = new CongesApiFactory();
        var sophie = factory.CreateClientAs(Sophie);

        var response = await Approve(sophie, 2);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var request = await sophie.GetFromJsonAsync<LeaveRequestDto>("/api/leave-requests/2", Json.Options);
        Assert.Equal(LeaveStatus.Pending, request!.Status);
    }

    [Fact]
    public async Task A_manager_cannot_approve_their_own_request()
    {
        using var factory = new CongesApiFactory();

        var response = await Approve(factory.CreateClientAs(Hugo), 3);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_parent_team_manager_approves_a_manager_request()
    {
        using var factory = new CongesApiFactory();

        var response = await Approve(factory.CreateClientAs(Claire), 3);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Approving_twice_is_a_conflict_and_debits_once()
    {
        using var factory = new CongesApiFactory();
        var hugo = factory.CreateClientAs(Hugo);
        await Approve(hugo, 1);

        var response = await Approve(hugo, 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NotPending", await SubmitLeaveRequestTests.ProblemCode(response));
        Assert.Equal(25, await BalanceOf(hugo, Lea));
    }

    [Fact]
    public async Task An_already_decided_request_cannot_be_approved()
    {
        using var factory = new CongesApiFactory();

        var response = await Approve(factory.CreateClientAs(Sophie), 5);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NotPending", await SubmitLeaveRequestTests.ProblemCode(response));
    }

    [Fact]
    public async Task Approving_an_unknown_request_returns_404()
    {
        using var factory = new CongesApiFactory();

        var response = await Approve(factory.CreateClientAs(Hugo), 9999);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
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

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("InsufficientBalance", await SubmitLeaveRequestTests.ProblemCode(response));
        Assert.Equal(1, await BalanceOf(hugo, Ines));
    }

    [Fact]
    public async Task The_root_team_manager_approves_their_own_request()
    {
        using var factory = new CongesApiFactory();
        var claire = factory.CreateClientAs(Claire);
        var submitted = await Read(await claire.PostAsJsonAsync("/api/leave-requests",
            new { type = "PaidLeave", startDate = "2027-03-08", endDate = "2027-03-12" }, Json.Options));

        var response = await Approve(claire, submitted.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Claire, (await Read(response)).DecidedById);
        Assert.Equal(25, await BalanceOf(claire, Claire));
    }

    private static Task<HttpResponseMessage> Reject(HttpClient client, int id, object body) =>
        client.PostAsJsonAsync($"/api/leave-requests/{id}/reject", body);

    [Fact]
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

    public static TheoryData<object> MissingReasons => new() { new { reason = "" }, new { reason = "   " }, new { } };

    [Theory]
    [MemberData(nameof(MissingReasons))]
    public async Task Rejecting_requires_a_reason(object body)
    {
        using var factory = new CongesApiFactory();

        var response = await Reject(factory.CreateClientAs(Hugo), 2, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await SubmitLeaveRequestTests.HasError(response, "reason"), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_reason_longer_than_500_characters_is_rejected()
    {
        using var factory = new CongesApiFactory();

        var response = await Reject(factory.CreateClientAs(Hugo), 2, new { reason = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_manager_of_another_team_cannot_reject()
    {
        using var factory = new CongesApiFactory();

        var response = await Reject(factory.CreateClientAs(Sophie), 2, new { reason = "Non" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_already_decided_request_cannot_be_rejected()
    {
        using var factory = new CongesApiFactory();

        var response = await Reject(factory.CreateClientAs(Sophie), 6, new { reason = "Non" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NotPending", await SubmitLeaveRequestTests.ProblemCode(response));
    }

    [Fact]
    public async Task Rejecting_an_unknown_request_returns_404()
    {
        using var factory = new CongesApiFactory();

        var response = await Reject(factory.CreateClientAs(Hugo), 9999, new { reason = "Non" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_root_team_manager_rejects_their_own_request()
    {
        using var factory = new CongesApiFactory();
        var claire = factory.CreateClientAs(Claire);
        var submitted = await Read(await claire.PostAsJsonAsync("/api/leave-requests",
            new { type = "PaidLeave", startDate = "2027-03-08", endDate = "2027-03-12" }, Json.Options));

        var response = await Reject(claire, submitted.Id, new { reason = "Finalement non" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Approving_requires_the_user_header()
    {
        using var factory = new CongesApiFactory();

        var response = await Approve(factory.CreateClient(), 1);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
