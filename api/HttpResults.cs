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

    /// <summary>
    /// Signed in, but an admin has not let this account onto the board yet.
    /// Carries a machine-readable code so the client can show the waiting
    /// screen rather than a generic "not allowed", which would be misleading —
    /// nothing is wrong, the request is simply early.
    /// </summary>
    public static IActionResult PendingApproval() =>
        new ObjectResult(new
        {
            code = "pending_approval",
            message = "Dein Zugang wurde noch nicht freigegeben.",
        })
        { StatusCode = StatusCodes.Status403Forbidden };
}
