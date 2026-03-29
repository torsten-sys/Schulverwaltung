using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Raeume;

public class IndexModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public IndexModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Suche        { get; set; }
    [BindProperty(SupportsGet = true)] public int?    FilterTyp    { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterStatus { get; set; }

    public IReadOnlyList<RaumZeile> Raeume    { get; set; } = [];
    public IReadOnlyList<RaumTyp>   RaumTypen { get; set; } = [];

    public bool DarfBearbeiten
    {
        get
        {
            var b = HttpContext.Items["AppBenutzer"] as AppBenutzer;
            return b != null && (b.AppRolle == 3 || (b.AppRolle >= 1 && b.DarfInventarVerwalten)) && !b.Gesperrt;
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        bool darfInventar = benutzer != null && (benutzer.AppRolle == 3 || (benutzer.AppRolle >= 1 && benutzer.DarfInventarVerwalten));
        if (!darfInventar) return RedirectToPage("/Zugriff/KeinZugriff");

        RaumTypen = await _db.RaumTypen.OrderBy(t => t.Reihenfolge).ToListAsync();

        var query = _db.Raeume.Include(r => r.RaumTyp).AsQueryable();

        if (FilterTyp.HasValue)
            query = query.Where(r => r.RaumTypId == FilterTyp.Value);
        if (FilterStatus == "gesperrt")
            query = query.Where(r => r.Gesperrt);
        else if (FilterStatus == "aktiv")
            query = query.Where(r => !r.Gesperrt);
        if (!string.IsNullOrWhiteSpace(Suche))
        {
            var s = Suche.Trim().ToLower();
            query = query.Where(r => r.RaumNr.ToLower().Contains(s) || r.Bezeichnung.ToLower().Contains(s));
        }

        var liste = await query.OrderBy(r => r.RaumTyp.Reihenfolge).ThenBy(r => r.RaumNr).ToListAsync();
        var raumIds = liste.Select(r => r.RaumId).ToList();
        var invCounts = await _db.Inventar
            .Where(i => i.RaumId != null && raumIds.Contains(i.RaumId!.Value))
            .GroupBy(i => i.RaumId!.Value)
            .Select(g => new { RaumId = g.Key, Count = g.Count() })
            .ToListAsync();
        var countDict = invCounts.ToDictionary(x => x.RaumId, x => x.Count);

        Raeume = liste.Select(r => new RaumZeile(
            r.RaumId, r.RaumNr, r.Bezeichnung, r.RaumTyp?.Bezeichnung ?? "",
            r.RaumTyp?.IstInternat ?? false, r.Kapazitaet,
            r.Gesperrt, countDict.GetValueOrDefault(r.RaumId, 0)
        )).ToList();
        return Page();
    }

    public record RaumZeile(int RaumId, string RaumNr, string Bezeichnung, string TypBezeichnung,
        bool IstInternat, int? Kapazitaet, bool Gesperrt, int AnzahlInventar);
}
