using System.Net;
using System.Net.Http.Json;
using System.Text;
using Conges.Application.LeaveRequests;
using Conges.Domain.LeaveRequests;

namespace Conges.Tests;

/// <summary>US3 — Refuser une demande avec un motif (POST /api/leave-requests/{id}/reject). Demandes seedées n° 1 à 8.</summary>
public sealed class RejectLeaveRequestApiTests : ApiTestBase
{
    private const int Hugo = 2, Karim = 4, Sophie = 6;

    private Task<HttpResponseMessage> RejectAsync(int employeeId, int requestId, string? reason) =>
        As(employeeId).PostAsJsonAsync($"/api/leave-requests/{requestId}/reject", new { reason });

    private Task<HttpResponseMessage> RejectRawAsync(int employeeId, int requestId, HttpContent? content) =>
        As(employeeId).PostAsync($"/api/leave-requests/{requestId}/reject", content);

    private static StringContent RawJson(string json) => new(json, Encoding.UTF8, "application/json");

    // ---------- Cas nominal (sc. 1, 5)

    [Fact]
    public async Task RG13_RG14_rejection_records_the_reason_and_keeps_the_balance()
    {
        var response = await RejectAsync(Hugo, 2, "Période de forte activité");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rejected = await ReadLeaveAsync(response);
        Assert.Equal(LeaveStatus.Rejected, rejected.Status);
        Assert.Equal("Période de forte activité", rejected.RejectionReason);
        Assert.Equal(Hugo, rejected.DecidedById);
        Assert.Equal(Clock.Now, rejected.DecidedAt);
        Assert.Equal(30, await BalanceOfAsync(Karim));
    }

    [Fact]
    public async Task RG13_reason_of_exactly_ten_characters_is_accepted()
    {
        var response = await RejectAsync(Hugo, 2, "Trop court");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RG13_reason_is_stored_without_leading_and_trailing_spaces()
    {
        var response = await RejectAsync(Hugo, 2, "  Trop court  ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Trop court", (await ReadLeaveAsync(response)).RejectionReason);
    }

    // ---------- Motif obligatoire (RG-13, sc. 2 à 4)

    [Theory]
    [InlineData("Trop tôt")]           // sc. 2 : 8 caractères
    [InlineData("   Trop tôt   ")]     // sc. 3 : 8 caractères une fois réduit
    [InlineData("")]                   // sc. 4
    [InlineData("     ")]              // sc. 4
    [InlineData(null)]                 // sc. 4
    public async Task RG13_reason_shorter_than_ten_characters_is_refused(string? reason)
    {
        var response = await RejectAsync(Hugo, 2, reason);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_REJECTION_REASON_REQUIRED");
        var request = await Anonymous().GetFromJsonAsync<LeaveRequestDto>("/api/leave-requests/2", Json.Options);
        Assert.Equal(LeaveStatus.Pending, request!.Status);
    }

    [Fact]
    public async Task RG13_empty_json_object_means_no_reason()
    {
        var response = await RejectRawAsync(Hugo, 2, RawJson("{}"));

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_REJECTION_REASON_REQUIRED");
    }

    [Fact]
    public async Task RG13_missing_body_without_content_type_means_no_reason()
    {
        var response = await RejectRawAsync(Hugo, 2, null);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_REJECTION_REASON_REQUIRED");
    }

    [Fact]
    public async Task RG13_empty_json_body_means_no_reason()
    {
        var response = await RejectRawAsync(Hugo, 2, RawJson(""));

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "LEAVE_REJECTION_REASON_REQUIRED");
    }

    // ---------- Habilitation, statut, existence (sc. 6 à 8)

    [Fact]
    public async Task RG10_only_the_approver_can_reject()
    {
        var response = await RejectAsync(Sophie, 1, "Période de forte activité");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
    }

    [Fact]
    public async Task RG11_decided_request_cannot_be_rejected()
    {
        var response = await RejectAsync(Sophie, 6, "Période de forte activité");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Conflict, "LEAVE_ALREADY_DECIDED");
    }

    [Fact]
    public async Task NF_unknown_request_cannot_be_rejected()
    {
        var response = await RejectAsync(Hugo, 9999, "Période de forte activité");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.NotFound, "LEAVE_NOT_FOUND");
    }

    // ---------- Ordre des vérifications (FR-024, sc. 9, 10)

    [Fact]
    public async Task RG11_RG13_status_is_checked_before_the_reason()
    {
        var response = await RejectAsync(Sophie, 6, "Trop tôt");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Conflict, "LEAVE_ALREADY_DECIDED");
    }

    [Fact]
    public async Task RG10_RG11_habilitation_is_checked_before_the_status()
    {
        // Demande n° 5 : déjà approuvée, et Hugo n'en est pas le valideur (Sophie l'est).
        var response = await RejectAsync(Hugo, 5, "Période de forte activité");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Forbidden, "LEAVE_APPROVER_NOT_ALLOWED");
    }

    [Fact]
    public async Task ERR_unidentified_user_cannot_reject()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/leave-requests/2/reject", new { reason = "Période de forte activité" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(9999)] // le 400 de forme passe avant le 404
    public async Task ERR_invalid_json_is_a_validation_error(int requestId)
    {
        var response = await RejectRawAsync(Hugo, requestId, RawJson("""{"reason":"""));

        await ProblemAssert.IsValidationProblemAsync(response);
    }

    // ---------- Décision concurrente (Edge Cases, FR-020)

    [Fact]
    public async Task RG11_concurrent_decision_is_refused()
    {
        using var factory = new CongesApiFactory { Clock = Clock, DbInterceptors = [new ConcurrentDecisionInterceptor(2)] };

        var response = await factory.CreateClientAs(Hugo)
            .PostAsJsonAsync("/api/leave-requests/2/reject", new { reason = "Période de forte activité" });

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Conflict, "LEAVE_ALREADY_DECIDED");
    }
}
