using Conges.Application.Common;

namespace Conges.Api.Http;

/// <summary>
/// Identification simplifiée pour l'atelier : l'employé courant est porté par l'en-tête <c>X-User-Id</c>.
/// </summary>
public sealed class HeaderCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string HeaderName = "X-User-Id";

    public int EmployeeId =>
        int.TryParse(accessor.HttpContext?.Request.Headers[HeaderName].ToString(), out var id) && id > 0
            ? id
            : throw new UnauthenticatedException();
}
