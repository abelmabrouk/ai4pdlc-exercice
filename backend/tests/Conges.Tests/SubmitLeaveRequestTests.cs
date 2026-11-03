using System.Net;
using System.Net.Http.Json;
using Conges.Application.Employees;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;
using static Conges.Tests.Problems;

namespace Conges.Tests;

/// <summary>Soumission d'une demande de congé (US1, RG-01 à RG-07). Chaque test part d'une base neuve.</summary>
public sealed class SubmitLeaveRequestTests
{
    private const int Claire = 1, Lea = 3, Karim = 4, Ines = 5;

    /// <summary>Lundi 1er mars 2027, 10 h : base des tests du délai de prévenance.</summary>
    private static readonly DateTimeOffset Monday = new(2027, 3, 1, 10, 0, 0, TimeSpan.Zero);

    internal static Task<HttpResponseMessage> Submit(HttpClient client, string type, string start, string end, string? comment = null) =>
        client.PostAsJsonAsync("/api/leave-requests", new { type, startDate = start, endDate = end, comment }, Json.Options);

    [Fact] // RG-02, RG-07
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

    [Fact] // RG-02 : jours ouvrables, le samedi compte
    public async Task Saturdays_are_counted()
    {
        using var factory = new CongesApiFactory();

        var response = await Submit(factory.CreateClientAs(Lea), "PaidLeave", "2027-03-08", "2027-03-13");

        var created = await response.Content.ReadFromJsonAsync<LeaveRequestDto>(Json.Options);
        Assert.Equal(6, created!.Days);
    }

    [Theory] // RG-01, RG-02
    [InlineData("2027-03-12", "2027-03-08")] // fin avant début
    [InlineData("2020-01-06", "2020-01-10")] // début dans le passé
    [InlineData("2027-03-14", "2027-03-14")] // dimanche seul : aucun jour décompté
    public async Task Invalid_periods_are_rejected(string start, string end)
    {
        using var factory = new CongesApiFactory();

        var response = await Submit(factory.CreateClientAs(Lea), "PaidLeave", start, end);

        await AssertProblem(response, (HttpStatusCode)422, "LEAVE_INVALID_PERIOD");
    }

    [Theory] // RG-03 : soumission un lundi → début au plus tôt le jeudi
    [InlineData("2027-03-02")] // mardi
    [InlineData("2027-03-03")] // mercredi
    public async Task Paid_leave_needs_two_working_days_of_notice(string start)
    {
        using var factory = new CongesApiFactory { Now = Monday };

        var response = await Submit(factory.CreateClientAs(Karim), "PaidLeave", start, "2027-03-05");

        await AssertProblem(response, (HttpStatusCode)422, "LEAVE_NOTICE_TOO_SHORT");
    }

    [Fact] // RG-03
    public async Task Paid_leave_starting_after_two_working_days_is_accepted()
    {
        using var factory = new CongesApiFactory { Now = Monday };

        var response = await Submit(factory.CreateClientAs(Karim), "PaidLeave", "2027-03-04", "2027-03-05");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory] // RG-04 : pas de délai de prévenance pour un événement familial
    [InlineData("2027-03-01")] // le jour même
    [InlineData("2027-03-02")] // le lendemain
    public async Task Family_events_have_no_notice_period(string start)
    {
        using var factory = new CongesApiFactory { Now = Monday };

        var response = await Submit(factory.CreateClientAs(Karim), "FamilyEvent", start, start);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact] // RG-05
    public async Task Paid_leave_beyond_the_available_balance_is_rejected()
    {
        using var factory = new CongesApiFactory();

        // Ines : solde 3, dont 2 jours déjà engagés par la demande seedée 7 en attente.
        var response = await Submit(factory.CreateClientAs(Ines), "PaidLeave", "2027-03-08", "2027-03-09");

        await AssertProblem(response, (HttpStatusCode)422, "LEAVE_INSUFFICIENT_BALANCE");
    }

    [Fact] // RG-05
    public async Task Family_event_is_accepted_regardless_of_the_balance()
    {
        using var factory = new CongesApiFactory();

        var response = await Submit(factory.CreateClientAs(Ines), "FamilyEvent", "2027-03-08", "2027-03-12");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact] // RG-06
    public async Task Overlapping_requests_are_rejected()
    {
        using var factory = new CongesApiFactory();
        var client = factory.CreateClientAs(Lea);
        await Submit(client, "PaidLeave", "2027-03-08", "2027-03-12");

        var response = await Submit(client, "PaidLeave", "2027-03-12", "2027-03-16");

        await AssertProblem(response, (HttpStatusCode)422, "LEAVE_OVERLAP");
    }

    [Fact] // RG-06 : les demandes refusées sont ignorées
    public async Task A_rejected_request_does_not_block_the_period()
    {
        using var factory = new CongesApiFactory();
        var lea = factory.CreateClientAs(Lea);
        var first = await (await Submit(lea, "PaidLeave", "2027-03-08", "2027-03-12")).Content
            .ReadFromJsonAsync<LeaveRequestDto>(Json.Options);
        await factory.CreateClientAs(2).PostAsJsonAsync($"/api/leave-requests/{first!.Id}/reject",
            new { reason = "Période de forte activité" });

        var response = await Submit(lea, "PaidLeave", "2027-03-08", "2027-03-12");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact] // Circuit de validation : la Direction est validée hors application
    public async Task The_root_team_manager_cannot_submit()
    {
        using var factory = new CongesApiFactory();

        var response = await Submit(factory.CreateClientAs(Claire), "PaidLeave", "2027-03-08", "2027-03-12");

        await AssertProblem(response, HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
    }

    [Fact] // Erreur de forme : 400 sans code
    public async Task A_comment_longer_than_500_characters_is_a_shape_error()
    {
        using var factory = new CongesApiFactory();

        var response = await Submit(factory.CreateClientAs(Lea), "PaidLeave", "2027-03-08", "2027-03-12", new string('x', 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await HasError(response, "comment"));
    }

    [Fact] // ADR-005
    public async Task Submitting_requires_the_user_header()
    {
        using var factory = new CongesApiFactory();

        var response = await Submit(factory.CreateClient(), "PaidLeave", "2027-03-08", "2027-03-12");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
