using System.Net;
using System.Net.Http.Json;
using System.Text;
using Conges.Domain.LeaveRequests;

namespace Conges.Tests;

/// <summary>US1 — Soumettre une demande de congé (POST /api/leave-requests). « Aujourd'hui » : lundi 2027-03-01.</summary>
public sealed class SubmitLeaveRequestApiTests : ApiTestBase
{
    private const string Route = "/api/leave-requests";

    private const int Claire = 1, Lea = 3, Karim = 4, Ines = 5;

    private Task<HttpResponseMessage> SubmitAsync(int employeeId, string type, string start, string end, string? comment = null) =>
        As(employeeId).PostAsJsonAsync(Route, new { type, startDate = start, endDate = end, comment });

    private static StringContent RawJson(string json) => new(json, Encoding.UTF8, "application/json");

    // ---------- Cas nominal (sc. 1, 2)

    [Fact]
    public async Task RG02_RG07_paid_leave_is_created_pending_with_its_working_days()
    {
        var response = await SubmitAsync(Lea, "PaidLeave", "2027-03-08", "2027-03-12", "Vacances");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await ReadLeaveAsync(response);
        Assert.Equal(Lea, created.EmployeeId);
        Assert.Equal(LeaveType.PaidLeave, created.Type);
        Assert.Equal(new DateOnly(2027, 3, 8), created.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 12), created.EndDate);
        Assert.Equal(5, created.Days);
        Assert.Equal(LeaveStatus.Pending, created.Status);
        Assert.Equal("Vacances", created.Comment);
        Assert.Equal(Clock.Now, created.SubmittedAt);
        Assert.Null(created.DecidedAt);
    }

    [Fact]
    public async Task RG12_submission_does_not_change_the_paid_leave_balance()
    {
        var response = await SubmitAsync(Lea, "PaidLeave", "2027-03-08", "2027-03-12");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(30, await BalanceOfAsync(Lea));
    }

    [Fact]
    public async Task RG07_created_request_can_be_read_back()
    {
        var response = await SubmitAsync(Lea, "PaidLeave", "2027-03-08", "2027-03-12", "Vacances");
        var created = await ReadLeaveAsync(response);

        var read = await Anonymous().GetFromJsonAsync<Application.LeaveRequests.LeaveRequestDto>(response.Headers.Location, Json.Options);

        Assert.Equal(created, read);
    }

    [Fact]
    public async Task RG10_general_management_can_submit_a_request()
    {
        var response = await SubmitAsync(Claire, "PaidLeave", "2027-03-08", "2027-03-12");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(LeaveStatus.Pending, (await ReadLeaveAsync(response)).Status);
    }

    // ---------- Décompte en jours ouvrables (RG-02, sc. 3 à 10)

    [Theory]
    [InlineData("PaidLeave", "2027-03-08", "2027-03-13", 6)]   // sc. 3 : le samedi est ouvrable
    [InlineData("PaidLeave", "2027-03-11", "2027-03-16", 5)]   // sc. 4 : jeu, ven, sam, lun, mar
    [InlineData("PaidLeave", "2027-03-08", "2027-03-14", 6)]   // sc. 5 : le dimanche n'est jamais décompté
    [InlineData("PaidLeave", "2027-05-03", "2027-05-08", 3)]   // sc. 6 : Ascension, fermeture et 8 mai exclus
    [InlineData("PaidLeave", "2027-05-17", "2027-05-17", 1)]   // sc. 7 : lundi de Pentecôte travaillé
    [InlineData("FamilyEvent", "2027-03-11", "2027-03-16", 5)] // sc. 10 : même décompte pour tous les types
    public async Task RG02_days_are_counted_in_working_days(string type, string start, string end, int expected)
    {
        var response = await SubmitAsync(Lea, type, start, end);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(expected, (await ReadLeaveAsync(response)).Days);
    }

    [Theory]
    [InlineData("2027-03-14")] // sc. 8 : un dimanche seul
    [InlineData("2027-05-06")] // sc. 9 : l'Ascension seule
    public async Task RG02_period_without_any_counted_day_is_invalid(string day)
    {
        var response = await SubmitAsync(Lea, "PaidLeave", day, day);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_INVALID_PERIOD");
    }

    // ---------- Période (RG-01, sc. 11, 12)

    [Fact]
    public async Task RG01_end_before_start_is_a_business_error_not_a_format_error()
    {
        var response = await SubmitAsync(Lea, "PaidLeave", "2027-03-12", "2027-03-08");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_INVALID_PERIOD");
    }

    [Theory]
    [InlineData("PaidLeave")]
    [InlineData("FamilyEvent")]
    public async Task RG01_start_in_the_past_is_invalid(string type)
    {
        var response = await SubmitAsync(Lea, type, "2027-02-28", "2027-03-02");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_INVALID_PERIOD");
    }

    // ---------- Délai de prévenance (RG-03, RG-04, sc. 13 à 17)

    [Theory]
    [InlineData("2027-03-01", "2027-03-04")] // sc. 13 : mardi et mercredi sont les deux jours ouvrés de prévenance
    [InlineData("2027-03-04", "2027-03-09")] // sc. 15 : vendredi et lundi
    [InlineData("2027-05-04", "2027-05-11")] // sc. 16 : mercredi 5 et lundi 10
    public async Task RG03_paid_leave_with_two_business_days_of_notice_is_accepted(string today, string start)
    {
        Clock.Now = TestClock.At(today).Now;

        var response = await SubmitAsync(Lea, "PaidLeave", start, start);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("2027-03-01", "2027-03-03")] // sc. 14 : mardi seul
    [InlineData("2027-03-04", "2027-03-08")] // sc. 15 : vendredi seul, le samedi ne compte pas
    [InlineData("2027-05-04", "2027-05-10")] // sc. 16 : l'Ascension et la fermeture du 7 mai ne comptent pas
    [InlineData("2027-03-01", "2027-03-01")] // Edge Cases : congés payés commençant le jour même
    public async Task RG03_paid_leave_without_two_business_days_of_notice_is_refused(string today, string start)
    {
        Clock.Now = TestClock.At(today).Now;

        var response = await SubmitAsync(Lea, "PaidLeave", start, start);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_NOTICE_TOO_SHORT");
    }

    [Fact]
    public async Task RG04_family_event_can_start_on_the_submission_day()
    {
        var response = await SubmitAsync(Lea, "FamilyEvent", "2027-03-01", "2027-03-01");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------- Solde disponible (RG-05, sc. 18 à 20) : Ines a 3 jours, dont 2 en attente (demande seedée n° 7)

    [Fact]
    public async Task RG05_paid_leave_exceeding_the_available_balance_is_refused()
    {
        var response = await SubmitAsync(Ines, "PaidLeave", "2027-03-08", "2027-03-09");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_INSUFFICIENT_BALANCE");
    }

    [Fact]
    public async Task RG05_paid_leave_equal_to_the_available_balance_is_accepted()
    {
        var response = await SubmitAsync(Ines, "PaidLeave", "2027-03-08", "2027-03-08");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RG05_family_event_is_not_limited_by_the_balance()
    {
        var response = await SubmitAsync(Ines, "FamilyEvent", "2027-03-08", "2027-03-10");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------- Chevauchement (RG-06, sc. 21 à 26) : Lea a déjà une demande du 2027-03-08 au 2027-03-12

    private async Task<int> LeaWeekOfMarch8Async(LeaveStatus status = LeaveStatus.Pending)
    {
        var id = (await ReadLeaveAsync(await SubmitAsync(Lea, "PaidLeave", "2027-03-08", "2027-03-12"))).Id;
        if (status != LeaveStatus.Pending)
        {
            await Factory.ArrangeAsync(async db => (await db.LeaveRequests.FindAsync(id))!.Status = status);
        }
        return id;
    }

    [Fact]
    public async Task RG06_request_sharing_a_single_day_with_a_pending_request_is_refused()
    {
        await LeaWeekOfMarch8Async();

        var response = await SubmitAsync(Lea, "PaidLeave", "2027-03-12", "2027-03-13");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_OVERLAP");
    }

    [Fact]
    public async Task RG06_contiguous_request_is_accepted()
    {
        await LeaWeekOfMarch8Async();

        var response = await SubmitAsync(Lea, "PaidLeave", "2027-03-13", "2027-03-13");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RG06_overlap_applies_whatever_the_type()
    {
        await LeaWeekOfMarch8Async();

        var response = await SubmitAsync(Lea, "FamilyEvent", "2027-03-10", "2027-03-10");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_OVERLAP");
    }

    [Fact]
    public async Task RG06_request_overlapping_an_approved_request_is_refused()
    {
        await LeaWeekOfMarch8Async(LeaveStatus.Approved);

        var response = await SubmitAsync(Lea, "PaidLeave", "2027-03-10", "2027-03-10");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_OVERLAP");
    }

    [Fact]
    public async Task RG06_rejected_requests_are_ignored()
    {
        await LeaWeekOfMarch8Async(LeaveStatus.Rejected);

        var response = await SubmitAsync(Lea, "PaidLeave", "2027-03-08", "2027-03-12");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RG06_requests_of_other_employees_are_ignored()
    {
        Assert.Equal(HttpStatusCode.Created, (await SubmitAsync(Karim, "PaidLeave", "2027-03-08", "2027-03-12")).StatusCode);

        var response = await SubmitAsync(Lea, "PaidLeave", "2027-03-08", "2027-03-12");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------- Ordre des vérifications (FR-024, sc. 30)

    [Fact]
    public async Task RG03_RG05_notice_is_checked_before_the_balance()
    {
        // Ines : 1 jour disponible ; 2 jours demandés, et seul le mardi est un jour ouvré de prévenance.
        var response = await SubmitAsync(Ines, "PaidLeave", "2027-03-03", "2027-03-04");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_NOTICE_TOO_SHORT");
    }

    [Fact]
    public async Task RG05_RG06_balance_is_checked_before_the_overlap()
    {
        // Ines : 1 jour disponible ; 2 jours demandés sur sa demande en attente n° 7 (2027-04-26 → 2027-04-27).
        var response = await SubmitAsync(Ines, "PaidLeave", "2027-04-26", "2027-04-27");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_INSUFFICIENT_BALANCE");
    }

    // ---------- Forme (sc. 27)

    [Theory]
    [InlineData("""{"startDate":"2027-03-08","endDate":"2027-03-12"}""")]
    [InlineData("""{"type":"Holiday","startDate":"2027-03-08","endDate":"2027-03-12"}""")]
    [InlineData("""{"type":42,"startDate":"2027-03-08","endDate":"2027-03-12"}""")]
    [InlineData("""{"type":"PaidLeave","endDate":"2027-03-12"}""")]
    [InlineData("""{"type":"PaidLeave","startDate":"2027-03-08"}""")]
    [InlineData("""{"type":"PaidLeave","startDate":"08/03/2027","endDate":"2027-03-12"}""")]
    [InlineData("""{"type":"PaidLeave",""")]
    public async Task ERR_malformed_submission_is_a_validation_error_without_code(string json)
    {
        var response = await As(Lea).PostAsync(Route, RawJson(json));

        await ProblemAssert.IsValidationProblemAsync(response);
    }

    // ---------- Identification (sc. 28)

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("999")]
    public async Task ERR_unidentified_user_cannot_submit(string? userId)
    {
        var client = Anonymous();
        if (userId is not null) client.DefaultRequestHeaders.Add("X-User-Id", userId);

        var response = await client.PostAsJsonAsync(Route, new { type = "PaidLeave", startDate = "2027-03-08", endDate = "2027-03-12" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("999")]
    public async Task ERR_identification_is_checked_before_the_body(string? userId)
    {
        var client = Anonymous();
        if (userId is not null) client.DefaultRequestHeaders.Add("X-User-Id", userId);

        var response = await client.PostAsync(Route, RawJson("""{"type":"Holiday"}"""));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
