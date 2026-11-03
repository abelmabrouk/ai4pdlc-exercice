namespace Conges.Application.Common;

public class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' introuvable.")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}

public class UnauthenticatedException()
    : Exception("En-tête X-User-Id absent ou invalide.");

/// <summary>Nature d'une erreur métier ; le gestionnaire global en déduit le statut HTTP (ADR-003).</summary>
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
/// Erreur métier porteuse d'un code stable du référentiel « Codes d'erreur » du wiki (ADR-003).
/// Le message est destiné aux journaux et au champ <c>detail</c> ; les clients se fondent uniquement sur le code.
/// </summary>
public class BusinessException(string code, BusinessErrorKind kind, string title, string message) : Exception(message)
{
    public string Code { get; } = code;
    public BusinessErrorKind Kind { get; } = kind;
    public string Title { get; } = title;
}
