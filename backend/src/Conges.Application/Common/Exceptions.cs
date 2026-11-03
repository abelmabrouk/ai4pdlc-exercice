namespace Conges.Application.Common;

public class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' introuvable.")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}

public class UnauthenticatedException()
    : Exception("En-tête X-User-Id absent ou invalide.");

/// <summary>Nature d'une erreur métier, traduite en statut HTTP par la couche Api (ADR-003).</summary>
public enum BusinessErrorKind
{
    /// <summary>Règle de gestion non respectée (422).</summary>
    RuleViolation,

    /// <summary>Action interdite à cet utilisateur (403).</summary>
    Forbidden,

    /// <summary>Ressource absente (404).</summary>
    NotFound,

    /// <summary>Conflit d'état (409).</summary>
    Conflict,
}

/// <summary>
/// Erreur métier porteuse d'un code stable du référentiel (wiki « Codes d'erreur »), traduite en ProblemDetails
/// avec l'extension <c>code</c> par le gestionnaire global (ADR-003).
/// </summary>
public class BusinessRuleException(string code, BusinessErrorKind kind, string title, string? detail = null)
    : Exception(title)
{
    public string Code { get; } = code;
    public BusinessErrorKind Kind { get; } = kind;
    public string Title { get; } = title;
    public string? Detail { get; } = detail;
}
