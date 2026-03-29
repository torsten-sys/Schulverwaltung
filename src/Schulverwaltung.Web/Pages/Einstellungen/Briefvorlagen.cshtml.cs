using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Einstellungen;

public class BriefvorlagenModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public BriefvorlagenModel(SchulverwaltungDbContext db) => _db = db;

    public List<Briefvorlage> Vorlagen      { get; set; } = [];
    public bool               DarfBearbeiten { get; set; }
    public string?            Meldung        { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        DarfBearbeiten = true;
        Meldung = TempData["Meldung"] as string;
        Vorlagen = await _db.Briefvorlagen.OrderBy(v => v.Bezeichnung).ToListAsync();
        return Page();
    }
}
