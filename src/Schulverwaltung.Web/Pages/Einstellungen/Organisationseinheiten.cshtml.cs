using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Einstellungen;

public class OrganisationseinheitenModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public OrganisationseinheitenModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public bool Bearbeiten { get; set; }

    public List<OrgEinheitViewModel> Einheiten { get; set; } = [];

    [BindProperty] public List<OrgEinheitZeile> Zeilen { get; set; } = [];

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
            var doppelt = await _db.Organisationseinheiten.AnyAsync(o => o.Code == z.Code && o.OrgEinheitId != z.OrgEinheitId);
            if (doppelt) { TempData["Fehler"] = $"Code '{z.Code}' ist bereits vergeben."; return RedirectToPage(new { bearbeiten = true }); }
        }

        // Upsert
        var verwendetSet = (await _db.Inventar
            .Where(i => i.OrgEinheitId != null)
            .Select(i => i.OrgEinheitId!.Value)
            .Distinct()
            .ToListAsync()).ToHashSet();

        foreach (var z in Zeilen)
        {
            if (z.OrgEinheitId == 0)
            {
                _db.Organisationseinheiten.Add(new Organisationseinheit {
                    Code = z.Code, Bezeichnung = z.Bezeichnung,
                    Reihenfolge = z.Reihenfolge, Gesperrt = z.Gesperrt
                });
            }
            else
            {
                var einheit = await _db.Organisationseinheiten.FindAsync(z.OrgEinheitId);
                if (einheit == null) continue;
                einheit.Reihenfolge = z.Reihenfolge;
                einheit.Gesperrt    = z.Gesperrt;
                if (!verwendetSet.Contains(z.OrgEinheitId))
                {
                    einheit.Code        = z.Code;
                    einheit.Bezeichnung = z.Bezeichnung;
                }
            }
        }

        await _db.SaveChangesAsync();
        TempData["Meldung"] = "Organisationseinheiten gespeichert.";
        return RedirectToPage(new { bearbeiten = true });
    }

    public async Task<IActionResult> OnPostLoeschenAsync(int orgEinheitId)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var einheit = await _db.Organisationseinheiten.FindAsync(orgEinheitId);
        if (einheit == null) return RedirectToPage(new { bearbeiten = true });

        if (await _db.Inventar.AnyAsync(i => i.OrgEinheitId == orgEinheitId))
        {
            TempData["Fehler"] = $"'{einheit.Bezeichnung}' wird verwendet und kann nicht gelöscht werden.";
            return RedirectToPage(new { bearbeiten = true });
        }

        _db.Organisationseinheiten.Remove(einheit);
        await _db.SaveChangesAsync();
        TempData["Meldung"] = $"'{einheit.Bezeichnung}' wurde gelöscht.";
        return RedirectToPage(new { bearbeiten = true });
    }

    private async Task LadeDaten()
    {
        var einheiten = await _db.Organisationseinheiten
            .OrderBy(o => o.Reihenfolge).ThenBy(o => o.Bezeichnung)
            .ToListAsync();

        var verwendetSet = (await _db.Inventar
            .Where(i => i.OrgEinheitId != null)
            .Select(i => i.OrgEinheitId!.Value)
            .Distinct()
            .ToListAsync()).ToHashSet();

        Einheiten = einheiten.Select(o => new OrgEinheitViewModel(o, verwendetSet.Contains(o.OrgEinheitId))).ToList();
    }

    public record OrgEinheitViewModel(Organisationseinheit Einheit, bool IsVerwendet);

    public class OrgEinheitZeile
    {
        public int    OrgEinheitId { get; set; }
        public string Code        { get; set; } = "";
        public string Bezeichnung { get; set; } = "";
        public int    Reihenfolge { get; set; }
        public bool   Gesperrt    { get; set; }
    }
}
