using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Einstellungen;

public class LehrgangArtenModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public LehrgangArtenModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public bool Bearbeiten { get; set; }

    public List<LehrgangArtViewModel> Arten { get; set; } = [];

    [BindProperty] public List<LehrgangArtZeile> Zeilen { get; set; } = [];

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
            var doppelt = await _db.LehrgangArten.AnyAsync(a => a.Code == z.Code && a.ArtId != z.ArtId);
            if (doppelt) { TempData["Fehler"] = $"Code '{z.Code}' ist bereits vergeben."; return RedirectToPage(new { bearbeiten = true }); }
        }

        // Upsert
        var verwendetSet = (await _db.Lehrgaenge
            .Where(l => l.ArtId != null)
            .Select(l => l.ArtId!.Value)
            .Distinct()
            .ToListAsync()).ToHashSet();

        foreach (var z in Zeilen)
        {
            if (z.ArtId == 0)
            {
                _db.LehrgangArten.Add(new LehrgangArt {
                    Code = z.Code, Bezeichnung = z.Bezeichnung,
                    Reihenfolge = z.Reihenfolge, Gesperrt = z.Gesperrt
                });
            }
            else
            {
                var art = await _db.LehrgangArten.FindAsync(z.ArtId);
                if (art == null) continue;
                art.Reihenfolge = z.Reihenfolge;
                art.Gesperrt    = z.Gesperrt;
                if (!verwendetSet.Contains(z.ArtId))
                {
                    art.Code        = z.Code;
                    art.Bezeichnung = z.Bezeichnung;
                }
            }
        }

        await _db.SaveChangesAsync();
        TempData["Meldung"] = "Lehrgangsarten gespeichert.";
        return RedirectToPage(new { bearbeiten = true });
    }

    public async Task<IActionResult> OnPostLoeschenAsync(int artId)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var art = await _db.LehrgangArten.FindAsync(artId);
        if (art == null) return RedirectToPage(new { bearbeiten = true });

        if (await _db.Lehrgaenge.AnyAsync(l => l.ArtId == artId))
        {
            TempData["Fehler"] = $"'{art.Bezeichnung}' wird verwendet und kann nicht gelöscht werden.";
            return RedirectToPage(new { bearbeiten = true });
        }

        _db.LehrgangArten.Remove(art);
        await _db.SaveChangesAsync();
        TempData["Meldung"] = $"'{art.Bezeichnung}' wurde gelöscht.";
        return RedirectToPage(new { bearbeiten = true });
    }

    private async Task LadeDaten()
    {
        var arten = await _db.LehrgangArten
            .OrderBy(a => a.Reihenfolge).ThenBy(a => a.Bezeichnung)
            .ToListAsync();

        var verwendetSet = (await _db.Lehrgaenge
            .Where(l => l.ArtId != null)
            .Select(l => l.ArtId!.Value)
            .Distinct()
            .ToListAsync()).ToHashSet();

        Arten = arten.Select(a => new LehrgangArtViewModel(a, verwendetSet.Contains(a.ArtId))).ToList();
    }

    public record LehrgangArtViewModel(LehrgangArt Art, bool IsVerwendet);

    public class LehrgangArtZeile
    {
        public int    ArtId       { get; set; }
        public string Code        { get; set; } = "";
        public string Bezeichnung { get; set; } = "";
        public int    Reihenfolge { get; set; }
        public bool   Gesperrt    { get; set; }
    }
}
