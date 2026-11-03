using System.Net;
using System.Net.Http.Json;
using Conges.Domain.LeaveRequests;
using Conges.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Conges.Tests;

/// <summary>US2 — Approuver une demande (POST /api/leave-requests/{id}/approve). Demandes seedées n° 1 à 8.</summary>
public sealed class ApproveLeaveRequestApiTests : ApiTestBase
{
    private const int Claire = 1, Hugo = 2, Lea = 3, Karim = 4, Sophie = 6, Thomas = 7, Yasmine = 8;

    private Task<HttpResponseMessage> ApproveAsync(int employeeId, int requestId) =>
        As(employeeId).PostAsync($"/api/leave-requests/{requestId}/approve", null);

    // ---------- Cas nominaux (sc. 1 à 3)

    [Fact]
    public async Task RG10_RG12_RG14_approval_by_the_manager_debits_the_paid_leave_balance()
    {
        var response = await ApproveAsync(Hugo, 1);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var approved = await ReadLeaveAsync(response);
        Assert.Equal(LeaveStatus.Approved, approved.Status);
        Assert.Equal(Hugo, approved.DecidedById);
        Assert.Equal(Clock.Now, approved.DecidedAt);
        Assert.Equal(25, await BalanceOfAsync(Lea));
    }

    [Fact]
    public async Task RG12_approving_a_family_event_does_not_change_the_balance()
    {
        var response = await ApproveAsync(Sophie, 4);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(LeaveStatus.Approved, (await ReadLeaveAsync(response)).Status);
        Assert.Equal(30, await BalanceOfAsync(Thomas));
    }

    [Fact]
    public async Task RG10_request_of_a_manager_is_approved_by_the_manager_of_the_parent_team()
    {
        var response = await ApproveAsync(Claire, 3);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- Habilitation (RG-10, sc. 4 à 6, 9)

    [Theory]
    [InlineData(Hugo, 3)]   // sc. 4 : sa propre demande
    [InlineData(Claire, 1)] // sc. 5 : N+2, pas de délégation
    [InlineData(Sophie, 1)] // sc. 6 : manager d'une autre équipe
    [InlineData(Karim, 1)]  // sc. 6 : simple collègue
    public async Task RG10_only_the_approver_can_approve(int employeeId, int requestId)
    {
        var response = await ApproveAsync(employeeId, requestId);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
    }

    [Theory]
    [InlineData(Claire)]
    [InlineData(Hugo)]
    public async Task RG10_request_of_general_management_cannot_be_approved_in_the_application(int employeeId)
    {
        var claireRequest = new LeaveRequest
        {
            EmployeeId = Claire, Type = LeaveType.PaidLeave, StartDate = new DateOnly(2027, 3, 8),
            EndDate = new DateOnly(2027, 3, 12), Days = 5, Status = LeaveStatus.Pending, SubmittedAt = Clock.Now,
        };
        await Factory.ArrangeAsync(db => { db.LeaveRequests.Add(claireRequest); return Task.CompletedTask; });

        var response = await ApproveAsync(employeeId, claireRequest.Id);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
    }

    // ---------- Statut et existence (RG-11, sc. 7, 8)

    [Theory]
    [InlineData(5)] // déjà approuvée
    [InlineData(6)] // déjà refusée
    public async Task RG11_decided_request_cannot_be_approved_again(int requestId)
    {
        var response = await ApproveAsync(Sophie, requestId);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Conflict, "LEAVE_ALREADY_DECIDED");
        Assert.Equal(25, await BalanceOfAsync(Yasmine));
    }

    [Fact]
    public async Task NF_unknown_request_cannot_be_approved()
    {
        var response = await ApproveAsync(Hugo, 9999);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.NotFound, "LEAVE_NOT_FOUND");
    }

    // ---------- Identification et ordre des vérifications (FR-021, FR-024)

    [Fact]
    public async Task ERR_unidentified_user_cannot_approve()
    {
        var response = await Anonymous().PostAsync("/api/leave-requests/1/approve", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ERR_identification_is_checked_before_existence()
    {
        var response = await ApproveAsync(999, 9999);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RG10_RG11_habilitation_is_checked_before_the_status()
    {
        // Demande n° 5 : déjà approuvée, et Hugo n'en est pas le valideur (Sophie l'est).
        var response = await ApproveAsync(Hugo, 5);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
    }

    // ---------- Décisions concurrentes (Edge Cases, FR-020)

    [Fact]
    public async Task RG11_status_is_a_concurrency_token()
    {
        using var scopeA = Factory.Services.CreateScope();
        using var scopeB = Factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
        var requestA = await dbA.LeaveRequests.SingleAsync(r => r.Id == 1);
        var requestB = await dbB.LeaveRequests.SingleAsync(r => r.Id == 1);

        requestA.Status = LeaveStatus.Approved;
        await dbA.SaveChangesAsync();
        requestB.Status = LeaveStatus.Rejected;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    [Fact]
    public async Task RG11_concurrent_decision_is_refused_and_the_balance_is_not_debited()
    {
        using var factory = new CongesApiFactory { Clock = Clock, DbInterceptors = [new ConcurrentDecisionInterceptor(1)] };

        var response = await factory.CreateClientAs(Hugo).PostAsync("/api/leave-requests/1/approve", null);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Conflict, "LEAVE_ALREADY_DECIDED");
        var lea = await factory.CreateClient().GetFromJsonAsync<Application.Employees.EmployeeDto>("/api/employees/3", Json.Options);
        Assert.Equal(30, lea!.PaidLeaveBalance);
    }
}
