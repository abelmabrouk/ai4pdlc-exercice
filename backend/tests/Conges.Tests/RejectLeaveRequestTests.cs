using System.Net;
using System.Net.Http.Json;
using System.Text;
using Conges.Application.Employees;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;

namespace Conges.Tests;

/// <summary>
/// US3 — Refuser une demande avec un motif. Date du jour : lundi 2027-02-01. Demandes du seed :
/// #2 Karim (en attente, valideur Hugo), #3 Hugo (en attente, valideur Claire), #5 Yasmine (approuvée).
/// </summary>
public sealed class RejectLeaveRequestTests
{
    private const int Hugo = 2, Karim = 4, Sophie = 6;
    private const string ValidReason = "Période de forte activité";

    private static CongesApiFactory Factory() => new() { Today = new DateOnly(2027, 2, 1) };

    private static Task<HttpResponseMessage> Reject(HttpClient client, int id, string? reason) =>
        client.PostAsync($"/api/leave-requests/{id}/reject", JsonContent.Create(new { reason }));

    private static Task<HttpResponseMessage> RejectRaw(HttpClient client, int id, HttpContent? content) =>
        client.PostAsync($"/api/leave-requests/{id}/reject", content);

    private static async Task<LeaveRequestDto> Ok(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Statut {(int)response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<LeaveRequestDto>(Json.Options))!;
    }

    private static async Task<LeaveRequestDto> Get(CongesApiFactory factory, int id) =>
        (await factory.CreateClient().GetFromJsonAsync<LeaveRequestDto>($"/api/leave-requests/{id}", Json.Options))!;

    [Fact]
    public async Task RG10_RG13_RG14_rejecting_records_the_reason_and_the_decision_without_touching_the_balance()
    {
        using var factory = Factory();

        var rejected = await Ok(await Reject(factory.CreateClientAs(Hugo), 2, ValidReason));

        Assert.Equal(LeaveStatus.Rejected, rejected.Status);
        Assert.Equal(ValidReason, rejected.RejectionReason);
        Assert.Equal(Hugo, rejected.DecidedById);
        Assert.Equal(new DateTimeOffset(2027, 2, 1, 9, 0, 0, TimeSpan.Zero), rejected.DecidedAt);
        Assert.Equal(LeaveStatus.Rejected, (await Get(factory, 2)).Status);
        var karim = await factory.CreateClient().GetFromJsonAsync<EmployeeDto>($"/api/employees/{Karim}", Json.Options);
        Assert.Equal(30, karim!.PaidLeaveBalance);
    }

    public static TheoryData<string?> TooShortReasons => new()
    {
        null,
        "",
        "         ",
        "   Non merci  ", // 9 caractères une fois les espaces retirés
    };

    [Theory]
    [MemberData(nameof(TooShortReasons))]
    public async Task RG13_a_missing_or_too_short_reason_is_refused(string? reason)
    {
        using var factory = Factory();

        await ProblemAssert.HasCode(await Reject(factory.CreateClientAs(Hugo), 2, reason),
            HttpStatusCode.UnprocessableEntity, "LEAVE_REJECTION_REASON_REQUIRED");

        Assert.Equal(LeaveStatus.Pending, (await Get(factory, 2)).Status);
    }

    [Fact]
    public async Task RG13_an_empty_body_or_object_means_no_reason()
    {
        using var factory = Factory();
        var hugo = factory.CreateClientAs(Hugo);

        await ProblemAssert.HasCode(await RejectRaw(hugo, 2, null),
            HttpStatusCode.UnprocessableEntity, "LEAVE_REJECTION_REASON_REQUIRED");
        await ProblemAssert.HasCode(await RejectRaw(hugo, 2, new StringContent("", Encoding.UTF8, "application/json")),
            HttpStatusCode.UnprocessableEntity, "LEAVE_REJECTION_REASON_REQUIRED");
        await ProblemAssert.HasCode(await RejectRaw(hugo, 2, new StringContent("{}", Encoding.UTF8, "application/json")),
            HttpStatusCode.UnprocessableEntity, "LEAVE_REJECTION_REASON_REQUIRED");
    }

    [Fact]
    public async Task RG13_a_reason_of_exactly_ten_characters_once_trimmed_is_accepted_and_stored_trimmed()
    {
        using var factory = Factory();

        var rejected = await Ok(await Reject(factory.CreateClientAs(Hugo), 2, "  Pas validé  "));

        Assert.Equal("Pas validé", rejected.RejectionReason);
    }

    [Fact]
    public async Task RG11_a_decided_request_cannot_be_rejected()
    {
        using var factory = Factory();

        await ProblemAssert.HasCode(await Reject(factory.CreateClientAs(Sophie), 5, ValidReason),
            HttpStatusCode.Conflict, "LEAVE_ALREADY_DECIDED");
    }

    [Theory]
    [InlineData(Sophie, 2)] // pas la valideuse de Karim
    [InlineData(Hugo, 3)]   // sa propre demande
    public async Task RG10_only_the_approver_can_reject(int someone, int requestId)
    {
        using var factory = Factory();

        await ProblemAssert.HasCode(await Reject(factory.CreateClientAs(someone), requestId, ValidReason),
            HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");

        Assert.Equal(LeaveStatus.Pending, (await Get(factory, requestId)).Status);
    }

    [Fact]
    public async Task RG10_permission_is_checked_before_the_reason()
    {
        using var factory = Factory();

        await ProblemAssert.HasCode(await Reject(factory.CreateClientAs(Sophie), 2, "court"),
            HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
    }

    [Fact]
    public async Task Rejecting_an_unknown_request_returns_leave_not_found()
    {
        using var factory = Factory();

        await ProblemAssert.HasCode(await Reject(factory.CreateClientAs(Hugo), 9999, ValidReason),
            HttpStatusCode.NotFound, "LEAVE_NOT_FOUND");
    }

    [Fact]
    public async Task Rejecting_requires_an_identified_user()
    {
        using var factory = Factory();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Reject(factory.CreateClient(), 2, ValidReason)).StatusCode);
    }
}
