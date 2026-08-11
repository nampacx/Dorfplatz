using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BulletinBoard.Api;

public static class HttpResults
{
    /// <summary>
    /// A plain 403, deliberately not <c>ForbidResult</c>. ForbidResult delegates to the
    /// ASP.NET Core authentication stack to run a challenge, and this app registers no
    /// authentication schemes — Static Web Apps has already authenticated the caller and
    /// hands us the principal in <c>x-ms-client-principal</c>. Without a scheme,
    /// ForbidResult throws "No authenticationScheme was specified" and the caller sees a
    /// 500 instead of "you may not do that".
    /// </summary>
    public static IActionResult Forbidden() => new StatusCodeResult(StatusCodes.Status403Forbidden);
}
