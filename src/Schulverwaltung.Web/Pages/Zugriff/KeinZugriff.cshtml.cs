using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Pages.Zugriff;

[AllowAnonymous]
public class KeinZugriffModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public KeinZugriffModel(SchulverwaltungDbContext db) => _db = db;

    public string DisplayName { get; set; } = "";
    public string RolleText   { get; set; } = "";

    public async Task OnGetAsync()
    {
        var adName = User?.Identity?.Name ?? "";
        if (!string.IsNullOrEmpty(adName))
        {
            var benutzer = await _db.AppBenutzer
                .FirstOrDefaultAsync(b => b.AdBenutzername == adName);
            if (benutzer != null)
            {
                DisplayName = benutzer.DisplayName ?? adName;
                RolleText   = AppBenutzerService.RolleText(benutzer.AppRolle);
                return;
            }
        }
        DisplayName = adName;
        RolleText   = "–";
    }
}
