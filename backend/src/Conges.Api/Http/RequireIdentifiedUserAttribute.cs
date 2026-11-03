using Conges.Application.Common;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Conges.Api.Http;

/// <summary>
/// Exige un utilisateur identifié (en-tête <c>X-User-Id</c> valide et collaborateur existant), sinon 401.
/// </summary>
/// <remarks>
/// Filtre d'autorisation : il s'exécute avant la liaison du modèle, si bien que le 401 passe avant tout 400 de forme
/// (FR-024). Les exceptions remontent au <see cref="GlobalExceptionHandler"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireIdentifiedUserAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context) =>
        context.HttpContext.RequestServices
            .GetRequiredService<ICurrentUserGuard>()
            .EnsureIdentifiedAsync(context.HttpContext.RequestAborted);
}
