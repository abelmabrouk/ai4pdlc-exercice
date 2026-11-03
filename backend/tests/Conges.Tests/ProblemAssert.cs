using System.Net;
using System.Text.Json;

namespace Conges.Tests;

/// <summary>
/// Assertions sur les réponses d'erreur ProblemDetails (ADR-003).
/// </summary>
internal static class ProblemAssert
{
    /// <summary>Erreur métier : statut attendu, <c>application/problem+json</c> et extension <c>code</c>.</summary>
    public static async Task HasCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Statut attendu {(int)status}, obtenu {(int)response.StatusCode} : {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("code", out var actual), $"Extension « code » absente : {body}");
        Assert.Equal(code, actual.GetString());
    }

    /// <summary>Erreur de forme : 400, <c>application/problem+json</c>, <c>errors</c> présent et pas de <c>code</c>.</summary>
    public static async Task IsValidationProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Statut attendu 400, obtenu {(int)response.StatusCode} : {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("errors", out _), $"« errors » absent : {body}");
        Assert.False(json.RootElement.TryGetProperty("code", out _), $"Un 400 de forme ne doit pas porter de code : {body}");
    }
}
