using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class MeisterAuswertungModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public MeisterAuswertungModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int     Id            { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterPerson  { get; set; }

    public string LehrgangNr  { get; set; } = "";
    public string LehrgangBez { get; set; } = "";

    public List<PatientenHistorieZeile>  PatientenHistorie  { get; set; } = [];
    public List<SchuelerUebersichtZeile> SchuelerUebersicht { get; set; } = [];
    public List<NotenverteilungZeile>    Notenverteilung    { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var lehrgang = await _db.Lehrgaenge.FindAsync(Id);
        if (lehrgang == null) return NotFound();
        LehrgangNr  = lehrgang.LehrgangNr;
        LehrgangBez = lehrgang.Bezeichnung;

        await LadeAuswertungen();
        return Page();
    }

    private async Task LadeAuswertungen()
    {
        // ── Abschnitte (PV + Prüfung) ──────────────────────────────────────
        var abschnitte = await _db.MeisterAbschnitte
            .Where(a => a.LehrgangId == Id && a.AbschnittTyp >= 1)
            .OrderBy(a => a.Nummer)
            .ToListAsync();

        var zuordnungen = await _db.MeisterPatientenZuordnungen
            .Where(z => z.LehrgangId == Id)
            .Include(z => z.Termine)
            .ToListAsync();

        // ── Patientenhistorie ──────────────────────────────────────────────
        // Alle einzigartigen Patienten + ihre Beteiligungen pro Abschnitt
        var patientenIds = zuordnungen.Select(z => z.PatientPersonId).Distinct().ToList();

        PatientenHistorie = patientenIds.Select(pid =>
        {
            var ersteZ = zuordnungen.First(z => z.PatientPersonId == pid);
            var teilnahmen = abschnitte.Select(a =>
            {
                var z = zuordnungen.FirstOrDefault(x => x.PatientPersonId == pid && x.AbschnittId == a.AbschnittId);
                return new PatientenTeilnahme(
                    a.Nummer, a.Bezeichnung,
                    z != null,
                    z?.Meisterschueler1Name,
                    z?.Meisterschueler2Name,
                    z?.BuchungsStatus ?? 0);
            }).ToList();

            return new PatientenHistorieZeile(
                ersteZ.PatientPersonId, ersteZ.PatientPersonNr, ersteZ.PatientName,
                teilnahmen);
        }).OrderBy(p => p.PatientName).ToList();

        // Filter anwenden
        if (!string.IsNullOrWhiteSpace(FilterPerson))
        {
            var q = FilterPerson.Trim().ToLower();
            PatientenHistorie = PatientenHistorie
                .Where(p => p.PatientName.ToLower().Contains(q)
                    || p.Teilnahmen.Any(t => (t.MS1 ?? "").ToLower().Contains(q)
                                          || (t.MS2 ?? "").ToLower().Contains(q)))
                .ToList();
        }

        // ── Meisterschüler-Übersicht ──────────────────────────────────────
        var teilnehmer = await _db.LehrgangPersonen
            .Where(lp => lp.LehrgangId == Id && lp.Rolle == LehrgangPersonRolle.Teilnehmer)
            .Include(lp => lp.Person)
            .ToListAsync();

        var faecher = await _db.MeisterFaecher
            .Where(f => f.LehrgangId == Id).ToListAsync();

        var noten = await _db.MeisterNoten
            .Where(n => n.LehrgangId == Id).ToListAsync();

        SchuelerUebersicht = teilnehmer.Select(lp =>
        {
            var alsMS1 = zuordnungen.Count(z => z.Meisterschueler1PersonId == lp.PersonId);
            var alsMS2 = zuordnungen.Count(z => z.Meisterschueler2PersonId == lp.PersonId);
            var versorgungen = alsMS1 + alsMS2;

            var hilfsmittel = zuordnungen
                .Where(z => z.Meisterschueler1PersonId == lp.PersonId || z.Meisterschueler2PersonId == lp.PersonId)
                .SelectMany(z => z.Termine.Where(t => t.TerminTyp == 2 && t.HilfsmittelUebergeben == true))
                .Count();

            var pNoten = noten.Where(n => n.PersonId == lp.PersonId && n.Note.HasValue).ToList();
            decimal? gesamt = null;
            if (pNoten.Count == faecher.Count && faecher.Count > 0)
            {
                var fw = faecher.ToDictionary(f => f.FachId, f => f.Gewichtung);
                var sum = pNoten.Sum(n => (decimal)n.Note!.Value * (fw.TryGetValue(n.FachId, out var w) ? w : 1m));
                var gew = faecher.Sum(f => f.Gewichtung);
                if (gew > 0) gesamt = Math.Round(sum / gew, 1);
            }

            return new SchuelerUebersichtZeile(
                lp.PersonId, lp.Person.PersonNr,
                lp.Person.Nachname + ", " + lp.Person.Vorname,
                versorgungen, alsMS1, alsMS2, hilfsmittel, gesamt);
        }).OrderBy(s => s.PersonName).ToList();

        // ── Notenverteilung ──────────────────────────────────────────────
        Notenverteilung = faecher.OrderBy(f => f.Reihenfolge).Select(f =>
        {
            var fNoten = noten.Where(n => n.FachId == f.FachId && n.Note.HasValue).ToList();
            var offen  = noten.Count(n => n.FachId == f.FachId && !n.Note.HasValue);
            return new NotenverteilungZeile(
                f.Bezeichnung, f.Gewichtung,
                fNoten.Any() ? Math.Round((decimal)fNoten.Average(n => n.Note!.Value), 1) : null,
                fNoten.Any() ? fNoten.Min(n => n.Note!.Value) : (byte?)null,
                fNoten.Any() ? fNoten.Max(n => n.Note!.Value) : (byte?)null,
                offen);
        }).ToList();
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record PatientenTeilnahme(
    int Nummer, string AbschnittBez, bool Teilgenommen,
    string? MS1, string? MS2, byte BuchungsStatus);

public record PatientenHistorieZeile(
    int PersonId, string PersonNr, string PatientName,
    List<PatientenTeilnahme> Teilnahmen);

public record SchuelerUebersichtZeile(
    int PersonId, string PersonNr, string PersonName,
    int Versorgungen, int AlsMS1, int AlsMS2,
    int HilfsmittelUebergeben, decimal? Gesamtnote);

public record NotenverteilungZeile(
    string Fach, decimal Gewichtung,
    decimal? DurchschnittNote, byte? BesteNote, byte? SchlechtsteNote,
    int NochOffen);
