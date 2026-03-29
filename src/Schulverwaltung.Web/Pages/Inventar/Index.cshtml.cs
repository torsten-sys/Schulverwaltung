using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Pages.Inventar;

public class IndexModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public IndexModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Suche           { get; set; }
    [BindProperty(SupportsGet = true)] public int?    FilterKategorie  { get; set; }
    [BindProperty(SupportsGet = true)] public int?    FilterRaum       { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterZustand    { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterGesperrt   { get; set; }
    [BindProperty(SupportsGet = true)] public int?    FilterOrgEinheit { get; set; }

    public IReadOnlyList<InventarZeile>         Liste          { get; set; } = [];
    public IReadOnlyList<InventarKategorie>     Kategorien     { get; set; } = [];
    public IReadOnlyList<Schulverwaltung.Domain.Entities.Raum> Raeume { get; set; } = [];
    public IReadOnlyList<Organisationseinheit>  OrgEinheiten   { get; set; } = [];

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

        Kategorien  = await _db.InventarKategorien.OrderBy(k => k.Reihenfolge).ThenBy(k => k.Bezeichnung).ToListAsync();
        Raeume      = await _db.Raeume.OrderBy(r => r.RaumNr).ToListAsync();
        OrgEinheiten = await _db.Organisationseinheiten.OrderBy(o => o.Reihenfolge).ThenBy(o => o.Bezeichnung).ToListAsync();

        var query = _db.Inventar
            .Include(i => i.Kategorie)
            .Include(i => i.Raum)
            .Include(i => i.Person)
            .Include(i => i.OrgEinheit)
            .AsQueryable();

        if (FilterKategorie.HasValue)
            query = query.Where(i => i.KategorieId == FilterKategorie.Value);

        if (FilterRaum.HasValue)
            query = query.Where(i => i.RaumId == FilterRaum.Value);

        if (FilterOrgEinheit.HasValue)
            query = query.Where(i => i.OrgEinheitId == FilterOrgEinheit.Value);

        if (!string.IsNullOrEmpty(FilterZustand) && byte.TryParse(FilterZustand, out var z))
            query = query.Where(i => i.Zustand == z);

        // Default: hide gesperrt unless explicitly requested
        if (FilterGesperrt == "alle")
        {
            // show all
        }
        else if (FilterGesperrt == "gesperrt")
        {
            query = query.Where(i => i.Gesperrt);
        }
        else
        {
            // default: only active
            query = query.Where(i => !i.Gesperrt);
        }

        if (!string.IsNullOrWhiteSpace(Suche))
        {
            var s = Suche.Trim().ToLower();
            query = query.Where(i => i.InventarNr.ToLower().Contains(s)
                || i.Bezeichnung.ToLower().Contains(s)
                || (i.Typ != null && i.Typ.ToLower().Contains(s))
                || (i.Seriennummer != null && i.Seriennummer.ToLower().Contains(s)));
        }

        var items = await query.OrderBy(i => i.Kategorie.Reihenfolge).ThenBy(i => i.InventarNr).ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        Liste = items.Select(i => {
            var nw = i.WartungNaechstesDatum ?? InventarService.NaechsteWartungBerechnen(i);
            return new InventarZeile(
                i.InventarId,
                i.InventarNr,
                i.Bezeichnung,
                i.Typ,
                i.Kategorie?.Bezeichnung ?? "",
                i.OrgEinheit != null ? $"{i.OrgEinheit.Code} · {i.OrgEinheit.Bezeichnung}" : null,
                i.Raum != null ? $"{i.Raum.RaumNr} · {i.Raum.Bezeichnung}" : null,
                i.Person != null ? $"{i.Person.Nachname}, {i.Person.Vorname}" : null,
                i.Zustand,
                InventarService.ZustandText(i.Zustand),
                nw,
                i.Gesperrt
            );
        }).ToList();

        return Page();
    }

    public record InventarZeile(
        int       InventarId,
        string    InventarNr,
        string    Bezeichnung,
        string?   Typ,
        string    KategorieBezeichnung,
        string?   OrgEinheitAnzeige,
        string?   RaumAnzeige,
        string?   PersonName,
        byte      Zustand,
        string    ZustandText,
        DateOnly? NaechsteWartung,
        bool      Gesperrt);
}
