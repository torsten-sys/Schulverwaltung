using System.Security.Claims;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Middleware;

public class BenutzerMiddleware
{
    private readonly RequestDelegate _next;

    public BenutzerMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppBenutzerService benutzerService)
    {
        var path = context.Request.Path.Value ?? "";

        // Ausnahmen: Zugriff-Seiten und statische Dateien überspringen
        if (path.StartsWith("/Zugriff", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css",     StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js",      StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib",     StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images",  StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_",       StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Windows-Identity auslesen
        var identity = context.User?.Identity;
        if (identity == null || !identity.IsAuthenticated)
        {
            await _next(context);
            return;
        }

        var adName      = identity.Name ?? "";
        var displayName = context.User?.FindFirst("name")?.Value
                          ?? context.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                          ?? adName;
        var email = context.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        try
        {
            var benutzer = await benutzerService.BenutzerAnmeldenAsync(adName, displayName, email);

            // Benutzer in HttpContext.Items für Pages zugänglich machen
            context.Items["AppBenutzer"] = benutzer;
            context.Items["AppRolle"]    = benutzer.AppRolle;
            if (benutzer.PersonId.HasValue)
                context.Items["PersonId"] = benutzer.PersonId.Value;

            // AppRolle als Claim hinzufügen (für [Authorize(Policy="...")])
            var rolleText = AppBenutzerService.RolleText(benutzer.AppRolle);
            context.User?.AddIdentity(new ClaimsIdentity(new[]
            {
                new Claim("AppRolle", rolleText)
            }));
        }
        catch (UnauthorizedAccessException ex) when (ex.Message == "GESPERRT")
        {
            context.Response.Redirect("/Zugriff/Gesperrt");
            return;
        }

        await _next(context);
    }
}
