using System.Net;
using System.Net.Http.Json;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;

namespace Conges.Tests;

/// <summary>US4 — Lister les demandes à décider (GET /api/leave-requests/pending). Demandes seedées n° 1 à 8.</summary>
public sealed class PendingLeaveRequestsApiTests : ApiTestBase
{
    private const string Route = "/api/leave-requests/pending";

    private const int Claire = 1, Hugo = 2;

    private async Task<List<LeaveRequestDto>> PendingOfAsync(int employeeId) =>
        (await As(employeeId).GetFromJsonAsync<List<LeaveRequestDto>>(Route, Json.Options))!;

    [Theory]
    [InlineData(1, new[] { 3 })]       // sc. 2 : Claire, N+2 des managers (demande de Hugo)
    [InlineData(2, new[] { 1, 2, 7 })] // sc. 1 : Hugo, sans sa propre demande n° 3
    [InlineData(3, new int[0])]
    [InlineData(4, new int[0])]
    [InlineData(5, new int[0])]
    [InlineData(6, new[] { 4, 8 })]    // sc. 3 : Sophie, sans les demandes déjà décidées n° 5 et 6
    [InlineData(7, new int[0])]        // sc. 4 : Thomas n'est valideur d'aucune demande
    [InlineData(8, new int[0])]
    [InlineData(9, new int[0])]
    public async Task RG10_pending_list_matches_the_validation_circuit(int employeeId, int[] expectedIds)
    {
        var response = await As(employeeId).GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pending = (await response.Content.ReadFromJsonAsync<List<LeaveRequestDto>>(Json.Options))!;
        Assert.Equal(expectedIds, pending.Select(r => r.Id));
        Assert.All(pending, r => Assert.Equal(LeaveStatus.Pending, r.Status));
    }

    [Fact]
    public async Task RG11_decided_request_leaves_the_list()
    {
        await Factory.ArrangeAsync(async db => (await db.LeaveRequests.FindAsync(1))!.Status = LeaveStatus.Approved);

        Assert.Equal(new[] { 2, 7 }, (await PendingOfAsync(Hugo)).Select(r => r.Id));
    }

    [Fact]
    public async Task RG10_list_items_have_the_same_format_as_the_consultation_sorted_by_start_date()
    {
        var pending = await PendingOfAsync(Hugo);

        Assert.Equal(pending.OrderBy(r => r.StartDate).ThenBy(r => r.Id), pending);
        foreach (var item in pending)
        {
            var read = await Anonymous().GetFromJsonAsync<LeaveRequestDto>($"/api/leave-requests/{item.Id}", Json.Options);
            Assert.Equal(read, item);
        }
    }

    [Fact]
    public async Task RG10_request_of_general_management_appears_in_no_list()
    {
        var claireRequest = new LeaveRequest
        {
            EmployeeId = Claire, Type = LeaveType.PaidLeave, StartDate = new DateOnly(2027, 3, 8),
            EndDate = new DateOnly(2027, 3, 12), Days = 5, Status = LeaveStatus.Pending, SubmittedAt = Clock.Now,
        };
        await Factory.ArrangeAsync(db => { db.LeaveRequests.Add(claireRequest); return Task.CompletedTask; });

        for (var employeeId = 1; employeeId <= 9; employeeId++)
        {
            Assert.DoesNotContain(await PendingOfAsync(employeeId), r => r.Id == claireRequest.Id);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("999")]
    public async Task ERR_unidentified_user_has_no_pending_list(string? userId)
    {
        var client = Anonymous();
        if (userId is not null) client.DefaultRequestHeaders.Add("X-User-Id", userId);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
