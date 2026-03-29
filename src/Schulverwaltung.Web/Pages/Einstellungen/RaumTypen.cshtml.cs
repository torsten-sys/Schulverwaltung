using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Einstellungen;

public class RaumTypenModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public RaumTypenModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public bool Bearbeiten { get; set; }

    public List<RaumTypViewModel> Typen { get; set; } = [];

    [BindProperty] public List<RaumTypZeile> Zeilen { get; set; } = [];

    public string? Fehler  { get; set; }
    public string? Meldung { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        Fehler  = TempData["Fehler"]  as string;
        Meldung = TempData["Meldung"] as string;

        await LadeDaten();
        return Page();
    }

    public async Task<IActionResult> OnPostSpeichernAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        // Normalize + validate
        var codeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var z in Zeilen)
        {
            if (string.IsNullOrWhiteSpace(z.Code))
                { TempData["Fehler"] = "Code darf nicht leer sein."; return RedirectToPage(new { bearbeiten = true }); }
            if (string.IsNullOrWhiteSpace(z.Bezeichnung))
                { TempData["Fehler"] = "Bezeichnung darf nicht leer sein."; return RedirectToPage(new { bearbeiten = true }); }
            z.Code = z.Code.Trim().ToUpper();
            z.Bezeichnung = z.Bezeichnung.Trim();
            if (!codeSet.Add(z.Code))
                { TempData["Fehler"] = $"Code '{z.Code}' kommt mehrfach vor."; return RedirectToPage(new { bearbeiten = true }); }
        }

        // DB uniqueness check
        foreach (var z in Zeilen)
        {
            var doppelt = await _db.RaumTypen.AnyAsync(t => t.Code == z.Code && t.RaumTypId != z.RaumTypId);
            if (doppelt) { TempData["Fehler"] = $"Code '{z.Code}' ist bereits vergeben."; return RedirectToPage(new { bearbeiten = true }); }
        }

        // Upsert
        var verwendetSet = (await _db.Raeume
            .Select(r => r.RaumTypId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        foreach (var z in Zeilen)
        {
            if (z.RaumTypId == 0)
            {
                _db.RaumTypen.Add(new RaumTyp {
                    Code = z.Code, Bezeichnung = z.Bezeichnung,
                    Reihenfolge = z.Reihenfolge, IstInternat = z.IstInternat, Gesperrt = z.Gesperrt
                });
            }
            else
            {
                var typ = await _db.RaumTypen.FindAsync(z.RaumTypId);
                if (typ == null) continue;
                typ.Reihenfolge = z.Reihenfolge;
                typ.Gesperrt    = z.Gesperrt;
                if (!verwendetSet.Contains(z.RaumTypId))
                {
                    typ.Code        = z.Code;
                    typ.Bezeichnung = z.Bezeichnung;
                    typ.IstInternat = z.IstInternat;
                }
            }
        }

        await _db.SaveChangesAsync();
        TempData["Meldung"] = "Raumtypen gespeichert.";
        return RedirectToPage(new { bearbeiten = true });
    }

    public async Task<IActionResult> OnPostLoeschenAsync(int raumTypId)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var typ = await _db.RaumTypen.FindAsync(raumTypId);
        if (typ == null) return RedirectToPage(new { bearbeiten = true });

        if (await _db.Raeume.AnyAsync(r => r.RaumTypId == raumTypId))
        {
            TempData["Fehler"] = $"'{typ.Bezeichnung}' wird verwendet und kann nicht gelöscht werden.";
            return RedirectToPage(new { bearbeiten = true });
        }

        _db.RaumTypen.Remove(typ);
        await _db.SaveChangesAsync();
        TempData["Meldung"] = $"'{typ.Bezeichnung}' wurde gelöscht.";
        return RedirectToPage(new { bearbeiten = true });
    }

    private async Task LadeDaten()
    {
        var typen = await _db.RaumTypen
            .OrderBy(t => t.Reihenfolge).ThenBy(t => t.Bezeichnung)
            .ToListAsync();

        var verwendetSet = (await _db.Raeume
            .Select(r => r.RaumTypId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        Typen = typen.Select(t => new RaumTypViewModel(t, verwendetSet.Contains(t.RaumTypId))).ToList();
    }

    public record RaumTypViewModel(RaumTyp Typ, bool IsVerwendet);

    public class RaumTypZeile
    {
        public int    RaumTypId   { get; set; }
        public string Code        { get; set; } = "";
        public string Bezeichnung { get; set; } = "";
        public int    Reihenfolge { get; set; }
        public bool   IstInternat { get; set; }
        public bool   Gesperrt    { get; set; }
    }
}
