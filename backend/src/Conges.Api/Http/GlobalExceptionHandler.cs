using System.Text.Json;
using Conges.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Conges.Api.Http;

/// <summary>
/// Traduit les exceptions applicatives en réponses ProblemDetails (RFC 7807).
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => CamelCase(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "La requête est invalide.",
            },
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Ressource introuvable.",
                Detail = notFound.Message,
            },
            UnauthenticatedException unauthenticated => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Utilisateur non identifié.",
                Detail = unauthenticated.Message,
            },
            BusinessException business => new ProblemDetails
            {
                Status = business.Kind switch
                {
                    BusinessErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                    BusinessErrorKind.NotFound => StatusCodes.Status404NotFound,
                    BusinessErrorKind.Conflict => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status422UnprocessableEntity,
                },
                Type = $"https://conges.example/errors/{business.Code.ToLowerInvariant().Replace('_', '-')}",
                Title = business.Title,
                Detail = business.Message,
                Extensions = { ["code"] = business.Code },
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Une erreur inattendue est survenue.",
            },
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Erreur non gérée");
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary>Noms de champs alignés sur le JSON de l'API (camelCase), segment par segment : <c>Items[0].Name</c> → <c>items[0].name</c>.</summary>
    private static string CamelCase(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
