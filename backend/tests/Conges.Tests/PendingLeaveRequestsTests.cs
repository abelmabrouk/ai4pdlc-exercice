using System.Net;
using System.Net.Http.Json;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;
using Conges.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Conges.Tests;

/// <summary>
/// US4 — Lister les demandes à décider. Demandes en attente du seed, par date de début :
/// #4 Thomas, #1 Léa, #3 Hugo, #2 Karim, #8 Yasmine, #7 Inès.
/// </summary>
public sealed class PendingLeaveRequestsTests(CongesApiFactory factory) : IClassFixture<CongesApiFactory>
{
    private const int Claire = 1, Hugo = 2, Lea = 3, Sophie = 6;

    private static async Task<int[]> PendingIds(HttpClient client) =>
        (await client.GetFromJsonAsync<List<LeaveRequestDto>>("/api/leave-requests/pending", Json.Options))!
        .Select(r => r.Id).ToArray();

    [Fact]
    public async Task RG10_a_manager_sees_the_requests_of_their_team_sorted_by_start_date_but_not_their_own()
    {
        var pending = await factory.CreateClientAs(Hugo)
            .GetFromJsonAsync<List<LeaveRequestDto>>("/api/leave-requests/pending", Json.Options);

        Assert.Equal([1, 2, 7], pending!.Select(r => r.Id));
        Assert.All(pending!, r => Assert.Equal(LeaveStatus.Pending, r.Status));
    }

    [Fact]
    public async Task RG10_the_parent_team_manager_only_sees_requests_of_managers_without_delegation() =>
        Assert.Equal([3], await PendingIds(factory.CreateClientAs(Claire)));

    [Fact]
    public async Task RG10_each_manager_sees_the_requests_of_their_own_team() =>
        Assert.Equal([4, 8], await PendingIds(factory.CreateClientAs(Sophie)));

    [Fact]
    public async Task An_employee_who_validates_nobody_gets_an_empty_list() =>
        Assert.Empty(await PendingIds(factory.CreateClientAs(Lea)));

    [Fact]
    public async Task Listing_requires_an_identified_user() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/leave-requests/pending")).StatusCode);

    [Fact]
    public async Task RG11_a_decided_request_leaves_the_list()
    {
        using var dedicated = new CongesApiFactory();
        var hugo = dedicated.CreateClientAs(Hugo);
        Assert.Equal([1, 2, 7], await PendingIds(hugo));

        using (var scope = dedicated.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.LeaveRequests.FindAsync(2))!.Status = LeaveStatus.Rejected;
            await db.SaveChangesAsync();
        }

        Assert.Equal([1, 7], await PendingIds(hugo));
    }
}
