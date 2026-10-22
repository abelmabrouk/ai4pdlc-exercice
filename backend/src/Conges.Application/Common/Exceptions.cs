namespace Conges.Application.Common;

public class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' introuvable.")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}

public class UnauthenticatedException()
    : Exception("En-tête X-User-Id absent ou invalide.");
