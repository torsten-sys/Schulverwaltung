using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Schulverwaltung.Infrastructure;
using Schulverwaltung.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddSchuleverwaltung(builder.Configuration);   // alle DI-Registrierungen

// Windows-Authentifizierung via Negotiate (Kerberos/NTLM)
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorization(options =>
{
    // Gast: nur Listen sehen
    options.AddPolicy("Gast", p =>
        p.RequireAuthenticatedUser());

    // Sachbearbeiter: Stammdaten + Lehrgänge pflegen
    options.AddPolicy("Sachbearbeiter", p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("AppRolle", "Sachbearbeiter") ||
            ctx.User.HasClaim("AppRolle", "Dozent") ||
            ctx.User.HasClaim("AppRolle", "Administrator")));

    // Dozent: Sachbearbeiter + Noten
    options.AddPolicy("Dozent", p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("AppRolle", "Dozent") ||
            ctx.User.HasClaim("AppRolle", "Administrator")));

    // Administrator: alles
    options.AddPolicy("Administrator", p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("AppRolle", "Administrator")));

    // Fallback: jeder authentifizierte AD-Benutzer kommt rein
    // (wird in Middleware weiter eingeschränkt)
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<BenutzerMiddleware>();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
