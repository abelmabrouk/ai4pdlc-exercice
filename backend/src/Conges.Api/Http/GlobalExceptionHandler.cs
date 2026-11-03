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
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "La requête est invalide.",
            },
            BusinessRuleException business => ToProblemDetails(business),
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

    /// <summary>Erreur métier → ProblemDetails avec l'extension <c>code</c> (ADR-003).</summary>
    private static ProblemDetails ToProblemDetails(BusinessRuleException exception) => new()
    {
        Status = exception.Kind switch
        {
            BusinessErrorKind.RuleViolation => StatusCodes.Status422UnprocessableEntity,
            BusinessErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            BusinessErrorKind.NotFound => StatusCodes.Status404NotFound,
            BusinessErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        },
        Type = $"https://conges.example/errors/{exception.Code.ToLowerInvariant().Replace('_', '-')}",
        Title = exception.Title,
        Detail = exception.Detail,
        Extensions = { ["code"] = exception.Code },
    };
}
