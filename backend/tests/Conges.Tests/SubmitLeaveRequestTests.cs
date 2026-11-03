using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Conges.Application.Employees;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;

namespace Conges.Tests;

/// <summary>Soumission d'une demande de congé (US1). Chaque test part d'une base neuve.</summary>
public sealed class SubmitLeaveRequestTests
{
    private const int Claire = 1, Lea = 3, Ines = 5;

    private static Task<HttpResponseMessage> Submit(HttpClient client, string type, string start, string end, string? comment = null) =>
        client.PostAsJsonAsync("/api/leave-requests", new { type, startDate = start, endDate = end, comment }, Json.Options);

    internal static async Task<string?> ProblemCode(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    internal static async Task<bool> HasError(HttpResponseMessage response, string field)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("errors", out var errors)
            && errors.EnumerateObject().Any(e => e.Name == field);
    }

    [Fact]
    public async Task Submitting_a_valid_request_creates_it_pending_without_touching_the_balance()
    {
        using var factory = new CongesApiFactory();
        var client = factory.CreateClientAs(Lea);

        var response = await Submit(client, "PaidLeave", "2027-03-08", "2027-03-12", "Vacances");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<LeaveRequestDto>(Json.Options);
        Assert.Equal(LeaveStatus.Pending, created!.Status);
        Assert.Equal(5, created.Days);
        Assert.Equal(Lea, created.EmployeeId);
        Assert.Equal("Vacances", created.Comment);
        Assert.Null(created.DecidedById);
        Assert.EndsWith($"/api/leave-requests/{created.Id}", response.Headers.Location!.ToString());

        var lea = await client.GetFromJsonAsync<EmployeeDto>($"/api/employees/{Lea}", Json.Options);
        Assert.Equal(30, lea!.PaidLeaveBalance);
    }

    [Theory]
    [InlineData("2027-03-12", "2027-03-08", "endDate")] // fin avant début
    [InlineData("2020-01-06", "2020-01-10", "startDate")] // début dans le passé
    [InlineData("2027-03-13", "2027-03-14", "endDate")] // aucun jour ouvré
    public async Task Invalid_periods_are_rejected(string start, string end, string field)
    {
        using var factory = new CongesApiFactory();

        var response = await Submit(factory.CreateClientAs(Lea), "PaidLeave", start, end);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await HasError(response, field), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_comment_longer_than_500_characters_is_rejected()
    {
        using var factory = new CongesApiFactory();

        var response = await Submit(factory.CreateClientAs(Lea), "PaidLeave", "2027-03-08", "2027-03-12", new string('x', 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Overlapping_requests_are_rejected()
    {
        using var factory = new CongesApiFactory();
        var client = factory.CreateClientAs(Lea);
        await Submit(client, "PaidLeave", "2027-03-08", "2027-03-12");

        var response = await Submit(client, "PaidLeave", "2027-03-10", "2027-03-16");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Overlap", await ProblemCode(response));
    }

    [Fact]
    public async Task Paid_leave_beyond_the_available_balance_is_rejected()
    {
        using var factory = new CongesApiFactory();

        // Ines : solde 3, dont 2 jours déjà engagés par la demande seedée 7 en attente.
        var response = await Submit(factory.CreateClientAs(Ines), "PaidLeave", "2027-03-08", "2027-03-09");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("InsufficientBalance", await ProblemCode(response));
    }

    [Fact]
    public async Task Family_event_is_accepted_regardless_of_the_balance()
    {
        using var factory = new CongesApiFactory();

        var response = await Submit(factory.CreateClientAs(Ines), "FamilyEvent", "2027-03-08", "2027-03-12");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task The_root_team_manager_request_is_created_pending()
    {
        using var factory = new CongesApiFactory();

        var response = await Submit(factory.CreateClientAs(Claire), "PaidLeave", "2027-03-08", "2027-03-12");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<LeaveRequestDto>(Json.Options);
        Assert.Equal(LeaveStatus.Pending, created!.Status);
    }

    [Fact]
    public async Task Submitting_requires_the_user_header()
    {
        using var factory = new CongesApiFactory();

        var response = await Submit(factory.CreateClient(), "PaidLeave", "2027-03-08", "2027-03-12");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
