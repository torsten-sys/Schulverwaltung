using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Einstellungen;

public class InventarKategorienModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public InventarKategorienModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public bool Bearbeiten { get; set; }

    public List<KategorieViewModel> Kategorien { get; set; } = [];

    [BindProperty] public List<KategorieZeile> Zeilen { get; set; } = [];

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
            var doppelt = await _db.InventarKategorien.AnyAsync(k => k.Code == z.Code && k.KategorieId != z.KategorieId);
            if (doppelt) { TempData["Fehler"] = $"Code '{z.Code}' ist bereits vergeben."; return RedirectToPage(new { bearbeiten = true }); }
        }

        // Upsert
        var verwendetSet = (await _db.Inventar
            .Select(i => i.KategorieId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        foreach (var z in Zeilen)
        {
            if (z.KategorieId == 0)
            {
                _db.InventarKategorien.Add(new InventarKategorie {
                    Code = z.Code, Bezeichnung = z.Bezeichnung,
                    Reihenfolge = z.Reihenfolge, Gesperrt = z.Gesperrt
                });
            }
            else
            {
                var kat = await _db.InventarKategorien.FindAsync(z.KategorieId);
                if (kat == null) continue;
                kat.Reihenfolge = z.Reihenfolge;
                kat.Gesperrt    = z.Gesperrt;
                if (!verwendetSet.Contains(z.KategorieId))
                {
                    kat.Code        = z.Code;
                    kat.Bezeichnung = z.Bezeichnung;
                }
            }
        }

        await _db.SaveChangesAsync();
        TempData["Meldung"] = "Inventarkategorien gespeichert.";
        return RedirectToPage(new { bearbeiten = true });
    }

    public async Task<IActionResult> OnPostLoeschenAsync(int kategorieId)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var kat = await _db.InventarKategorien.FindAsync(kategorieId);
        if (kat == null) return RedirectToPage(new { bearbeiten = true });

        if (await _db.Inventar.AnyAsync(i => i.KategorieId == kategorieId))
        {
            TempData["Fehler"] = $"'{kat.Bezeichnung}' wird verwendet und kann nicht gelöscht werden.";
            return RedirectToPage(new { bearbeiten = true });
        }

        _db.InventarKategorien.Remove(kat);
        await _db.SaveChangesAsync();
        TempData["Meldung"] = $"'{kat.Bezeichnung}' wurde gelöscht.";
        return RedirectToPage(new { bearbeiten = true });
    }

    private async Task LadeDaten()
    {
        var kategorien = await _db.InventarKategorien
            .OrderBy(k => k.Reihenfolge).ThenBy(k => k.Bezeichnung)
            .ToListAsync();

        var verwendetSet = (await _db.Inventar
            .Select(i => i.KategorieId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        Kategorien = kategorien.Select(k => new KategorieViewModel(k, verwendetSet.Contains(k.KategorieId))).ToList();
    }

    public record KategorieViewModel(InventarKategorie Kategorie, bool IsVerwendet);

    public class KategorieZeile
    {
        public int    KategorieId  { get; set; }
        public string Code        { get; set; } = "";
        public string Bezeichnung { get; set; } = "";
        public int    Reihenfolge { get; set; }
        public bool   Gesperrt    { get; set; }
    }
}
