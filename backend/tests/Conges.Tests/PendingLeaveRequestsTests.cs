using System.Net;
using System.Net.Http.Json;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;

namespace Conges.Tests;

/// <summary>Demandes en attente que l'utilisateur courant peut décider (US2).</summary>
public sealed class PendingLeaveRequestsTests(CongesApiFactory factory) : IClassFixture<CongesApiFactory>
{
    private const int Claire = 1, Hugo = 2, Lea = 3, Sophie = 6;

    private static async Task<List<LeaveRequestDto>> Pending(HttpClient client) =>
        (await client.GetFromJsonAsync<List<LeaveRequestDto>>("/api/leave-requests/pending", Json.Options))!;

    [Theory]
    [InlineData(Hugo, new[] { 1, 2, 7 })] // équipe Produit, sans sa propre demande 3
    [InlineData(Sophie, new[] { 4, 8 })] // équipe Support
    [InlineData(Claire, new[] { 3 })] // demande du manager Produit, équipe rattachée à la Direction
    [InlineData(Lea, new int[0])] // ne manage personne
    public async Task Only_requests_the_user_can_decide_are_listed(int userId, int[] expectedIds)
    {
        var pending = await Pending(factory.CreateClientAs(userId));

        Assert.Equal(expectedIds.Order(), pending.Select(r => r.Id).Order());
    }

    [Fact]
    public async Task Listed_requests_are_pending_and_sorted_by_start_date()
    {
        var pending = await Pending(factory.CreateClientAs(Hugo));

        Assert.All(pending, r => Assert.Equal(LeaveStatus.Pending, r.Status));
        Assert.Equal(pending.OrderBy(r => r.StartDate).ThenBy(r => r.Id).Select(r => r.Id), pending.Select(r => r.Id));
    }

    [Fact]
    public async Task The_root_team_manager_sees_their_own_request()
    {
        using var isolated = new CongesApiFactory();
        var claire = isolated.CreateClientAs(Claire);
        var submitted = await (await claire.PostAsJsonAsync("/api/leave-requests",
            new { type = "PaidLeave", startDate = "2027-03-08", endDate = "2027-03-12" }, Json.Options))
            .Content.ReadFromJsonAsync<LeaveRequestDto>(Json.Options);

        var pending = await Pending(claire);

        Assert.Equal(new[] { 3, submitted!.Id }.Order(), pending.Select(r => r.Id).Order());
    }

    [Fact]
    public async Task Listing_requires_the_user_header()
    {
        var response = await factory.CreateClient().GetAsync("/api/leave-requests/pending");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
