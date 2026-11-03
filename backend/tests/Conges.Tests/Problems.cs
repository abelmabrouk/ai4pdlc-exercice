using System.Net;
using System.Text.Json;

namespace Conges.Tests;

/// <summary>Assertions sur les ProblemDetails (ADR-003, référentiel « Codes d'erreur »).</summary>
internal static class Problems
{
    public static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Attendu {(int)status}, reçu {(int)response.StatusCode} : {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("code", out var actual), body);
        Assert.Equal(code, actual.GetString());
    }

    public static async Task<bool> HasError(HttpResponseMessage response, string field)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("errors", out var errors)
            && errors.EnumerateObject().Any(e => e.Name == field);
    }
}
