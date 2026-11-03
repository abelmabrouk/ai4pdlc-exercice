using System.Net;
using System.Text.Json;

namespace Conges.Tests;

/// <summary>
/// Assertions sur les réponses d'erreur ProblemDetails (ADR-003).
/// </summary>
internal static class ProblemAssert
{
    /// <summary>Erreur métier : statut attendu et extension <c>code</c> du référentiel.</summary>
    public static async Task HasCode(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Statut attendu {(int)status}, reçu {(int)response.StatusCode} : {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("code", out var actual), $"Extension 'code' absente : {body}");
        Assert.Equal(code, actual.GetString());
    }

    /// <summary>Erreur de forme : 400 avec <c>errors</c> et sans code métier.</summary>
    public static async Task IsValidationProblem(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Statut attendu 400, reçu {(int)response.StatusCode} : {body}");

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("errors", out _), $"Propriété 'errors' absente : {body}");
        Assert.False(json.RootElement.TryGetProperty("code", out _), $"Une erreur de forme ne porte pas de code : {body}");
    }
}
