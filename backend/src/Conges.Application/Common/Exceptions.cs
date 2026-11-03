namespace Conges.Application.Common;

public class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' introuvable.")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}

public class UnauthenticatedException()
    : Exception("En-tête X-User-Id absent ou invalide.");

/// <summary>L'utilisateur courant n'est pas autorisé à effectuer l'action (403).</summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>L'action est incompatible avec l'état actuel des données (409).</summary>
public class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class ConflictCodes
{
    public const string NotPending = nameof(NotPending);
    public const string Overlap = nameof(Overlap);
    public const string InsufficientBalance = nameof(InsufficientBalance);
    public const string ConcurrentDecision = nameof(ConcurrentDecision);
}
