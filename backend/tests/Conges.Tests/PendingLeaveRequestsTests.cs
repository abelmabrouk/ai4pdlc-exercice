using System.Net;
using System.Net.Http.Json;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;

namespace Conges.Tests;

/// <summary>Demandes en attente dont l'utilisateur courant est le valideur (US2, RG-10, circuit de validation).</summary>
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

    [Fact] // Circuit de validation : un manager ne voit jamais sa propre demande
    public async Task A_manager_never_sees_their_own_request()
    {
        var pending = await Pending(factory.CreateClientAs(Hugo));

        Assert.DoesNotContain(pending, r => r.EmployeeId == Hugo);
    }

    [Fact]
    public async Task Listing_requires_the_user_header()
    {
        var response = await factory.CreateClient().GetAsync("/api/leave-requests/pending");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
