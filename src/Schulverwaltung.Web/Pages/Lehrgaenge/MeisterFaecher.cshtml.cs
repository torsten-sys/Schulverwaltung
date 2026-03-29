using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class MeisterFaecherModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public MeisterFaecherModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int LehrgangId { get; set; }

    public Lehrgang?              Lehrgang { get; set; }
    public List<FachZeile>        Faecher  { get; set; } = [];
    public string?                Fehler   { get; set; }
    public string?                Erfolg   { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 2)
            return RedirectToPage("/Zugriff/KeinZugriff");
        if (benutzer.AppRolle == 2)
        {
            var darfZugreifen = await _db.LehrgangPersonen
                .AnyAsync(lp => lp.LehrgangId == LehrgangId
                             && lp.PersonId == benutzer.PersonId
                             && lp.Rolle == LehrgangPersonRolle.Dozent);
            if (!darfZugreifen)
                return RedirectToPage("/Zugriff/KeinZugriff");
        }

        await LadeDaten();
        return Page();
    }

    public async Task<IActionResult> OnPostSpeichernAsync(
        [FromForm] List<int>     fachIds,
        [FromForm] List<string>  bezeichnungen,
        [FromForm] List<decimal> gewichtungen,
        [FromForm] List<int>     reihenfolgen,
        [FromForm] List<string>  isNeu)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 2)
            return RedirectToPage("/Zugriff/KeinZugriff");
        if (benutzer.AppRolle == 2)
        {
            var darfZugreifen = await _db.LehrgangPersonen
                .AnyAsync(lp => lp.LehrgangId == LehrgangId
                             && lp.PersonId == benutzer.PersonId
                             && lp.Rolle == LehrgangPersonRolle.Dozent);
            if (!darfZugreifen)
                return RedirectToPage("/Zugriff/KeinZugriff");
        }

        await LadeLehrgang();
        if (Lehrgang == null) return NotFound();

        var belegNr = await NextNoAsync("AENDERUNG");
        var user    = User.Identity?.Name ?? "System";

        // Bestehende Fächer
        var bestehendeIds = fachIds.Where(id => id > 0).ToList();
        var bestehende    = await _db.MeisterFaecher
            .Where(f => f.LehrgangId == LehrgangId && bestehendeIds.Contains(f.FachId))
            .ToListAsync();

        for (int i = 0; i < fachIds.Count; i++)
        {
            var id     = fachIds[i];
            var bez    = i < bezeichnungen.Count ? bezeichnungen[i].Trim() : "";
            var gew    = i < gewichtungen.Count  ? gewichtungen[i]          : 1m;
            var reihe  = i < reihenfolgen.Count  ? reihenfolgen[i]          : i + 1;

            if (string.IsNullOrWhiteSpace(bez)) continue;

            if (id <= 0)
            {
                // Neues Fach
                var neuFach = new MeisterFach {
                    LehrgangId  = LehrgangId,
                    Bezeichnung = bez,
                    Gewichtung  = gew,
                    Reihenfolge = reihe
                };
                _db.MeisterFaecher.Add(neuFach);
                _db.LehrgangAenderungsposten.Add(new LehrgangAenderungsposten {
                    BelegNr             = belegNr,
                    LehrgangId          = LehrgangId,
                    LehrgangNr          = Lehrgang.LehrgangNr,
                    LehrgangBezeichnung = Lehrgang.Bezeichnung,
                    Ereignis            = "FachAngelegt",
                    Tabelle             = "MeisterFach",
                    Feld                = "Bezeichnung",
                    NeuerWert           = bez,
                    AusfuehrendUser     = user
                });
            }
            else
            {
                var fach = bestehende.FirstOrDefault(f => f.FachId == id);
                if (fach == null) continue;

                if (fach.Bezeichnung != bez)
                {
                    _db.LehrgangAenderungsposten.Add(new LehrgangAenderungsposten {
                        BelegNr             = belegNr,
                        LehrgangId          = LehrgangId,
                        LehrgangNr          = Lehrgang.LehrgangNr,
                        LehrgangBezeichnung = Lehrgang.Bezeichnung,
                        Ereignis            = "FachGeaendert",
                        Tabelle             = "MeisterFach",
                        Feld                = "Bezeichnung",
                        AlterWert           = fach.Bezeichnung,
                        NeuerWert           = bez,
                        AusfuehrendUser     = user
                    });
                    fach.Bezeichnung = bez;
                }
                if (fach.Gewichtung != gew)
                {
                    _db.LehrgangAenderungsposten.Add(new LehrgangAenderungsposten {
                        BelegNr             = belegNr,
                        LehrgangId          = LehrgangId,
                        LehrgangNr          = Lehrgang.LehrgangNr,
                        LehrgangBezeichnung = Lehrgang.Bezeichnung,
                        Ereignis            = "FachGeaendert",
                        Tabelle             = "MeisterFach",
                        Feld                = "Gewichtung",
                        AlterWert           = fach.Gewichtung.ToString("0.00"),
                        NeuerWert           = gew.ToString("0.00"),
                        AusfuehrendUser     = user
                    });
                    fach.Gewichtung = gew;
                }
                fach.Reihenfolge = reihe;
            }
        }

        await _db.SaveChangesAsync();
        Erfolg = "Fächer gespeichert.";
        await LadeDaten();
        return Page();
    }

    public async Task<IActionResult> OnPostLoeschenAsync(int fachId)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 2)
            return RedirectToPage("/Zugriff/KeinZugriff");
        if (benutzer.AppRolle == 2)
        {
            var darfZugreifen = await _db.LehrgangPersonen
                .AnyAsync(lp => lp.LehrgangId == LehrgangId
                             && lp.PersonId == benutzer.PersonId
                             && lp.Rolle == LehrgangPersonRolle.Dozent);
            if (!darfZugreifen)
                return RedirectToPage("/Zugriff/KeinZugriff");
        }

        var fach = await _db.MeisterFaecher
            .Include(f => f.Noten)
            .FirstOrDefaultAsync(f => f.FachId == fachId && f.LehrgangId == LehrgangId);

        if (fach == null) return NotFound();

        if (fach.Noten.Any(n => n.Note.HasValue))
        {
            Fehler = "Fach kann nicht gelöscht werden, da bereits Noten eingetragen sind.";
            await LadeDaten();
            return Page();
        }

        _db.MeisterNoten.RemoveRange(fach.Noten);
        _db.MeisterFaecher.Remove(fach);
        await _db.SaveChangesAsync();

        return RedirectToPage(new { lehrgangId = LehrgangId });
    }

    public async Task<IActionResult> OnPostVerschiebenAsync(int fachId, string richtung)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 2)
            return RedirectToPage("/Zugriff/KeinZugriff");
        if (benutzer.AppRolle == 2)
        {
            var darfZugreifen = await _db.LehrgangPersonen
                .AnyAsync(lp => lp.LehrgangId == LehrgangId
                             && lp.PersonId == benutzer.PersonId
                             && lp.Rolle == LehrgangPersonRolle.Dozent);
            if (!darfZugreifen)
                return RedirectToPage("/Zugriff/KeinZugriff");
        }

        var alle = await _db.MeisterFaecher
            .Where(f => f.LehrgangId == LehrgangId)
            .OrderBy(f => f.Reihenfolge)
            .ToListAsync();

        var idx = alle.FindIndex(f => f.FachId == fachId);
        if (idx < 0) return RedirectToPage(new { lehrgangId = LehrgangId });

        int swapIdx = richtung == "hoch" ? idx - 1 : idx + 1;
        if (swapIdx < 0 || swapIdx >= alle.Count) return RedirectToPage(new { lehrgangId = LehrgangId });

        (alle[idx].Reihenfolge, alle[swapIdx].Reihenfolge) = (alle[swapIdx].Reihenfolge, alle[idx].Reihenfolge);
        await _db.SaveChangesAsync();

        return RedirectToPage(new { lehrgangId = LehrgangId });
    }

    private async Task LadeLehrgang()
    {
        Lehrgang = await _db.Lehrgaenge.FirstOrDefaultAsync(l => l.LehrgangId == LehrgangId);
    }

    private async Task LadeDaten()
    {
        await LadeLehrgang();
        if (Lehrgang == null) return;

        var faecher = await _db.MeisterFaecher
            .Where(f => f.LehrgangId == LehrgangId)
            .OrderBy(f => f.Reihenfolge)
            .ToListAsync();

        // Prüfen welche Fächer benotete Noten haben (kein Löschen erlaubt)
        var benotet = await _db.MeisterNoten
            .Where(n => n.LehrgangId == LehrgangId && n.Note.HasValue)
            .Select(n => n.FachId)
            .Distinct()
            .ToListAsync();

        Faecher = faecher.Select((f, i) => new FachZeile(
            f.FachId, f.Bezeichnung, f.Gewichtung, f.Reihenfolge,
            benotet.Contains(f.FachId), i == 0, i == faecher.Count - 1
        )).ToList();
    }

    private async Task<string> NextNoAsync(string serieCode)
    {
        var zeile = await _db.NoSerieZeilen
            .Where(z => z.NoSerieCode == serieCode && z.Offen)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Keine offene Nummernserie für '{serieCode}'.");

        long naechste = zeile.LastNoUsed == null
            ? long.Parse(zeile.StartingNo[(zeile.Prefix?.Length ?? 0)..])
            : long.Parse(zeile.LastNoUsed[(zeile.Prefix?.Length ?? 0)..]) + zeile.IncrementBy;

        var nr = (zeile.Prefix ?? "") + naechste.ToString().PadLeft(zeile.NummerLaenge, '0');
        zeile.LastNoUsed   = nr;
        zeile.LastDateUsed = DateOnly.FromDateTime(DateTime.Today);
        return nr;
    }
}

public record FachZeile(
    int FachId, string Bezeichnung, decimal Gewichtung, int Reihenfolge,
    bool HatBenoteteNoten, bool IstErste, bool IstLetzte);
