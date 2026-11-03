using System.Net;
using System.Net.Http.Json;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;
using Conges.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Conges.Tests;

/// <summary>
/// US1 — Soumettre une demande de congé. Sauf mention contraire, la date du jour est le lundi 2027-02-01.
/// </summary>
public sealed class SubmitLeaveRequestTests
{
    private const int Lea = 3, Ines = 5;
    private static readonly DateOnly Monday20270201 = new(2027, 2, 1);

    private static CongesApiFactory Factory(DateOnly? today = null) => new() { Today = today ?? Monday20270201 };

    private static Task<HttpResponseMessage> Submit(HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/leave-requests", body);

    private static object PaidLeave(string start, string end, string? comment = null) =>
        new { type = "PaidLeave", startDate = start, endDate = end, comment };

    private static object FamilyEvent(string start, string end) =>
        new { type = "FamilyEvent", startDate = start, endDate = end };

    private static async Task<LeaveRequestDto> Created(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Statut {(int)response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<LeaveRequestDto>(Json.Options))!;
    }

    private static async Task<int> RequestCount(HttpClient client, int employeeId) =>
        (await client.GetFromJsonAsync<List<LeaveRequestDto>>($"/api/leave-requests?employeeId={employeeId}", Json.Options))!.Count;

    // --- Création et décompte ---

    [Fact]
    public async Task RG02_RG07_a_valid_request_is_created_pending_with_its_working_days()
    {
        using var factory = Factory();
        var client = factory.CreateClientAs(Lea);

        var response = await Submit(client, PaidLeave("2027-03-08", "2027-03-12", "Vacances"));
        var created = await Created(response);

        Assert.Equal(LeaveStatus.Pending, created.Status);
        Assert.Equal(5, created.Days);
        Assert.Equal(Lea, created.EmployeeId);
        Assert.Equal("Lea Petit", created.EmployeeName);
        Assert.Equal(LeaveType.PaidLeave, created.Type);
        Assert.Equal(new DateOnly(2027, 3, 8), created.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 12), created.EndDate);
        Assert.Equal("Vacances", created.Comment);
        Assert.Equal(new DateTimeOffset(2027, 2, 1, 9, 0, 0, TimeSpan.Zero), created.SubmittedAt);
        Assert.Null(created.DecidedAt);

        Assert.EndsWith($"/api/leave-requests/{created.Id}", response.Headers.Location?.ToString());
        var fetched = await client.GetFromJsonAsync<LeaveRequestDto>($"/api/leave-requests/{created.Id}", Json.Options);
        Assert.Equal(created, fetched);
    }

    [Theory]
    [InlineData("2027-03-08", "2027-03-13", 6)] // lundi → samedi : le samedi est ouvrable
    [InlineData("2027-03-12", "2027-03-15", 3)] // vendredi → lundi : le dimanche n'est jamais décompté
    [InlineData("2027-05-03", "2027-05-08", 3)] // Ascension, fermeture et Victoire 1945 exclues
    [InlineData("2027-05-17", "2027-05-17", 1)] // lundi de Pentecôte travaillé
    public async Task RG02_paid_leave_is_counted_in_working_days(string start, string end, int expectedDays)
    {
        using var factory = Factory();

        var created = await Created(await Submit(factory.CreateClientAs(Lea), PaidLeave(start, end)));

        Assert.Equal(expectedDays, created.Days);
    }

    [Fact]
    public async Task RG02_family_event_days_are_counted_like_paid_leave()
    {
        using var factory = Factory();

        var created = await Created(await Submit(factory.CreateClientAs(Lea), FamilyEvent("2027-03-08", "2027-03-13")));

        Assert.Equal(6, created.Days);
        Assert.Equal(LeaveType.FamilyEvent, created.Type);
    }

    // --- Période invalide ---

    [Fact]
    public async Task RG01_end_before_start_is_an_invalid_period_and_nothing_is_saved()
    {
        using var factory = Factory();
        var client = factory.CreateClientAs(Lea);
        var before = await RequestCount(client, Lea);

        var response = await Submit(client, PaidLeave("2027-03-12", "2027-03-08"));

        await ProblemAssert.HasCode(response, HttpStatusCode.UnprocessableEntity, "LEAVE_INVALID_PERIOD");
        Assert.Equal(before, await RequestCount(client, Lea));
    }

    [Fact]
    public async Task RG01_a_start_date_in_the_past_is_an_invalid_period()
    {
        using var factory = Factory();

        var response = await Submit(factory.CreateClientAs(Lea), FamilyEvent("2027-01-29", "2027-02-02"));

        await ProblemAssert.HasCode(response, HttpStatusCode.UnprocessableEntity, "LEAVE_INVALID_PERIOD");
    }

    [Theory]
    [InlineData("2027-03-14")] // dimanche
    [InlineData("2027-05-06")] // Ascension
    public async Task RG02_a_period_without_any_counted_day_is_invalid(string day)
    {
        using var factory = Factory();

        var response = await Submit(factory.CreateClientAs(Lea), PaidLeave(day, day));

        await ProblemAssert.HasCode(response, HttpStatusCode.UnprocessableEntity, "LEAVE_INVALID_PERIOD");
    }

    // --- Délai de prévenance ---

    [Fact]
    public async Task RG03_paid_leave_needs_two_business_days_of_notice()
    {
        using var factory = Factory(new DateOnly(2027, 3, 1)); // lundi
        var client = factory.CreateClientAs(Lea);

        await ProblemAssert.HasCode(await Submit(client, PaidLeave("2027-03-03", "2027-03-03")),
            HttpStatusCode.UnprocessableEntity, "LEAVE_NOTICE_TOO_SHORT"); // seul mardi
        await Created(await Submit(client, PaidLeave("2027-03-04", "2027-03-04"))); // mardi et mercredi
    }

    [Fact]
    public async Task RG03_notice_ignores_holidays_closures_and_weekends()
    {
        using var factory = Factory(new DateOnly(2027, 5, 4)); // mardi
        var client = factory.CreateClientAs(Lea);

        await ProblemAssert.HasCode(await Submit(client, PaidLeave("2027-05-10", "2027-05-10")),
            HttpStatusCode.UnprocessableEntity, "LEAVE_NOTICE_TOO_SHORT"); // seul mercredi 5 mai
        await Created(await Submit(client, PaidLeave("2027-05-11", "2027-05-11"))); // mercredi 5 et lundi 10
    }

    [Fact]
    public async Task RG04_family_events_have_no_notice_period()
    {
        using var factory = Factory(new DateOnly(2027, 3, 1));
        var client = factory.CreateClientAs(Lea);

        await Created(await Submit(client, FamilyEvent("2027-03-01", "2027-03-01"))); // le jour même
        await Created(await Submit(client, FamilyEvent("2027-03-02", "2027-03-02"))); // le lendemain
    }

    // --- Solde ---

    [Fact]
    public async Task RG05_pending_paid_leave_is_deducted_from_the_available_balance()
    {
        // Inès : solde 3, demande #7 en attente pour 2 jours → 1 jour disponible.
        using var factory = Factory();
        var client = factory.CreateClientAs(Ines);

        await ProblemAssert.HasCode(await Submit(client, PaidLeave("2027-04-05", "2027-04-06")),
            HttpStatusCode.UnprocessableEntity, "LEAVE_INSUFFICIENT_BALANCE");
        await Created(await Submit(client, PaidLeave("2027-04-05", "2027-04-05")));
    }

    [Fact]
    public async Task RG05_family_events_are_not_limited_by_the_balance()
    {
        using var factory = Factory();

        var created = await Created(await Submit(factory.CreateClientAs(Ines), FamilyEvent("2027-04-05", "2027-04-09")));

        Assert.Equal(5, created.Days);
    }

    // --- Chevauchement ---

    [Fact]
    public async Task RG06_a_request_overlapping_a_pending_request_by_one_day_is_refused()
    {
        using var factory = Factory();
        var client = factory.CreateClientAs(Lea);
        await Created(await Submit(client, PaidLeave("2027-03-08", "2027-03-12")));

        var response = await Submit(client, PaidLeave("2027-03-12", "2027-03-15"));

        await ProblemAssert.HasCode(response, HttpStatusCode.UnprocessableEntity, "LEAVE_OVERLAP");
    }

    [Fact]
    public async Task RG06_a_request_overlapping_a_seeded_pending_request_is_refused()
    {
        // Demande #1 de Léa : 2027-03-01 → 2027-03-05, en attente.
        using var factory = Factory();

        var response = await Submit(factory.CreateClientAs(Lea), FamilyEvent("2027-03-05", "2027-03-05"));

        await ProblemAssert.HasCode(response, HttpStatusCode.UnprocessableEntity, "LEAVE_OVERLAP");
    }

    [Fact]
    public async Task RG06_a_request_overlapping_an_approved_request_is_refused()
    {
        // Les demandes approuvées du seed sont dans le passé : on approuve une demande future de Léa.
        using var factory = Factory();
        var client = factory.CreateClientAs(Lea);
        var first = await Created(await Submit(client, FamilyEvent("2027-04-12", "2027-04-16")));
        await SetStatus(factory, first.Id, LeaveStatus.Approved);

        var response = await Submit(client, FamilyEvent("2027-04-16", "2027-04-16"));

        await ProblemAssert.HasCode(response, HttpStatusCode.UnprocessableEntity, "LEAVE_OVERLAP");
    }

    [Fact]
    public async Task RG06_a_rejected_request_does_not_block_its_period()
    {
        using var factory = Factory();
        var client = factory.CreateClientAs(Lea);
        var first = await Created(await Submit(client, PaidLeave("2027-03-08", "2027-03-12")));
        await SetStatus(factory, first.Id, LeaveStatus.Rejected);

        await Created(await Submit(client, PaidLeave("2027-03-08", "2027-03-12")));
    }

    // --- Forme et identification ---

    public static TheoryData<string> MalformedBodies => new()
    {
        """{ "startDate": "2027-03-08", "endDate": "2027-03-12" }""",                     // type absent
        """{ "type": "Holiday", "startDate": "2027-03-08", "endDate": "2027-03-12" }""",  // type inconnu
        """{ "type": "PaidLeave", "endDate": "2027-03-12" }""",                           // début absent
        """{ "type": "PaidLeave", "startDate": "2027-03-08" }""",                         // fin absente
        """{ "type": "PaidLeave", "startDate": "08/03/2027", "endDate": "2027-03-12" }""", // date mal formée
    };

    [Theory]
    [MemberData(nameof(MalformedBodies))]
    public async Task Malformed_requests_are_validation_problems_without_business_code(string json)
    {
        using var factory = Factory();

        var response = await factory.CreateClientAs(Lea).PostAsync("/api/leave-requests",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        await ProblemAssert.IsValidationProblem(response);
    }

    [Fact]
    public async Task Submitting_requires_an_identified_user()
    {
        using var factory = Factory();

        var response = await Submit(factory.CreateClient(), PaidLeave("2027-03-08", "2027-03-12"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task SetStatus(CongesApiFactory factory, int id, LeaveStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await db.LeaveRequests.FindAsync(id);
        request!.Status = status;
        await db.SaveChangesAsync();
    }
}
