using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class MeisterNotenTeilnehmerModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public MeisterNotenTeilnehmerModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int LehrgangId { get; set; }
    [BindProperty(SupportsGet = true)] public int PersonId   { get; set; }

    public Lehrgang?                   Lehrgang        { get; set; }
    public string                      PersonName      { get; set; } = "";
    public List<NotenFachZeile>        FachNoten       { get; set; } = [];
    public List<DozentenAuswahl>       DozentenListe   { get; set; } = [];
    public int                         VorherigePersonId { get; set; }
    public int                         NaechstePersonId  { get; set; }
    public int                         TeilnehmerNr      { get; set; }
    public int                         TeilnehmerGesamt  { get; set; }
    public decimal?                    Gesamtnote        { get; set; }
    public int                         BenoteteFaecher   { get; set; }
    public int                         GesamtFaecher     { get; set; }
    public bool                        AlleBenotet       => BenoteteFaecher == GesamtFaecher && GesamtFaecher > 0;
    public string?                     Fehler            { get; set; }

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
        [FromForm] List<int>     noteIds,
        [FromForm] List<int>     fachIds,
        [FromForm] List<string>  noten,
        [FromForm] List<int?>    dozentPersonIds,
        [FromForm] List<string>  bewertungsDaten,
        [FromForm] List<string>  notizen)
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

        var lehrgang = await _db.Lehrgaenge.FindAsync(LehrgangId);
        if (lehrgang == null) return NotFound();

        var person = await _db.LehrgangPersonen
            .Include(lp => lp.Person)
            .FirstOrDefaultAsync(lp => lp.LehrgangId == LehrgangId && lp.PersonId == PersonId
                                       && lp.Rolle == LehrgangPersonRolle.Teilnehmer);
        if (person == null) return NotFound();

        var belegNr = await NextNoAsync("AENDERUNG");
        var user    = User.Identity?.Name ?? "System";

        for (int i = 0; i < fachIds.Count; i++)
        {
            var fachId    = fachIds[i];
            var noteStr   = i < noten.Count ? noten[i] : "";
            var noteVal   = byte.TryParse(noteStr, out var nb) ? (byte?)nb : null;
            var dozentId  = i < dozentPersonIds.Count ? dozentPersonIds[i] : null;
            var datumStr  = i < bewertungsDaten.Count ? bewertungsDaten[i] : "";
            var notiz     = i < notizen.Count ? notizen[i] : "";
            var noteId    = i < noteIds.Count ? noteIds[i] : 0;

            DateOnly? bewDatum = DateOnly.TryParse(datumStr, out var bd) ? bd : null;

            MeisterNote? vorhandenNote = noteId > 0
                ? await _db.MeisterNoten.AsNoTracking().FirstOrDefaultAsync(n => n.NoteId == noteId)
                : null;

            if (vorhandenNote == null)
            {
                // Note für diesen Teilnehmer+Fach suchen
                vorhandenNote = await _db.MeisterNoten.AsNoTracking()
                    .FirstOrDefaultAsync(n => n.LehrgangId == LehrgangId && n.FachId == fachId && n.PersonId == PersonId);
            }

            if (noteVal == null && vorhandenNote == null) continue;

            // Dozent-Name ermitteln
            string? dozentName = null;
            if (dozentId.HasValue)
            {
                var doz = await _db.Personen.FindAsync(dozentId.Value);
                dozentName = doz != null ? $"{doz.Nachname}, {doz.Vorname}" : null;
            }

            if (vorhandenNote == null)
            {
                if (noteVal == null) continue;
                // Neu anlegen
                var fach = await _db.MeisterFaecher.FindAsync(fachId);
                var neu = new MeisterNote {
                    LehrgangId               = LehrgangId,
                    FachId                   = fachId,
                    PersonId                 = PersonId,
                    PersonNr                 = person.Person.PersonNr,
                    PersonName               = person.Person.Nachname + ", " + person.Person.Vorname,
                    Note                     = noteVal,
                    BewertendeDozentPersonId = dozentId,
                    BewertendeDozentName     = dozentName,
                    BewertungsDatum          = bewDatum,
                    Notiz                    = string.IsNullOrWhiteSpace(notiz) ? null : notiz,
                    CreatedBy                = user
                };
                _db.MeisterNoten.Add(neu);
                await _db.SaveChangesAsync();

                _db.MeisterNoteAenderungsposten.Add(new MeisterNoteAenderungsposten {
                    BelegNr              = belegNr,
                    NoteId               = neu.NoteId,
                    LehrgangId           = LehrgangId,
                    LehrgangNr           = lehrgang.LehrgangNr,
                    FachBezeichnung      = fach?.Bezeichnung ?? "",
                    PersonNr             = person.Person.PersonNr,
                    PersonName           = person.Person.Nachname + ", " + person.Person.Vorname,
                    AlteNote             = null,
                    NeueNote             = noteVal.Value,
                    BewertendeDozentName = dozentName,
                    Zeitstempel          = DateTime.UtcNow,
                    AusfuehrendUser      = user
                });
            }
            else
            {
                // Vergleich – nur bei Änderung schreiben
                var geaendert = vorhandenNote.Note != noteVal
                    || vorhandenNote.BewertendeDozentPersonId != dozentId
                    || vorhandenNote.BewertungsDatum != bewDatum
                    || vorhandenNote.Notiz != (string.IsNullOrWhiteSpace(notiz) ? null : notiz);

                if (geaendert)
                {
                    var dbNote = await _db.MeisterNoten.FindAsync(vorhandenNote.NoteId);
                    if (dbNote == null) continue;

                    var alteNote = dbNote.Note;
                    dbNote.Note                     = noteVal;
                    dbNote.BewertendeDozentPersonId = dozentId;
                    dbNote.BewertendeDozentName     = dozentName;
                    dbNote.BewertungsDatum          = bewDatum;
                    dbNote.Notiz                    = string.IsNullOrWhiteSpace(notiz) ? null : notiz;

                    if (alteNote != noteVal && noteVal.HasValue)
                    {
                        var fach = await _db.MeisterFaecher.FindAsync(fachId);
                        _db.MeisterNoteAenderungsposten.Add(new MeisterNoteAenderungsposten {
                            BelegNr              = belegNr,
                            NoteId               = dbNote.NoteId,
                            LehrgangId           = LehrgangId,
                            LehrgangNr           = lehrgang.LehrgangNr,
                            FachBezeichnung      = fach?.Bezeichnung ?? "",
                            PersonNr             = person.Person.PersonNr,
                            PersonName           = person.Person.Nachname + ", " + person.Person.Vorname,
                            AlteNote             = alteNote,
                            NeueNote             = noteVal.Value,
                            BewertendeDozentName = dozentName,
                            Zeitstempel          = DateTime.UtcNow,
                            AusfuehrendUser      = user
                        });
                    }
                }
            }
        }

        await _db.SaveChangesAsync();
        return RedirectToPage(new { lehrgangId = LehrgangId, personId = PersonId });
    }

    private async Task LadeDaten()
    {
        Lehrgang = await _db.Lehrgaenge.FindAsync(LehrgangId);
        if (Lehrgang == null) return;

        // Teilnehmer-Liste alphabetisch
        var teilnehmer = await _db.LehrgangPersonen
            .Include(lp => lp.Person)
            .Where(lp => lp.LehrgangId == LehrgangId && lp.Rolle == LehrgangPersonRolle.Teilnehmer && lp.Status != 2)
            .OrderBy(lp => lp.Person.Nachname).ThenBy(lp => lp.Person.Vorname)
            .Select(lp => new { lp.PersonId, Name = lp.Person.Nachname + ", " + lp.Person.Vorname,
                                lp.Person.PersonNr })
            .ToListAsync();

        TeilnehmerGesamt = teilnehmer.Count;
        var idx = teilnehmer.FindIndex(t => t.PersonId == PersonId);

        if (idx < 0 && teilnehmer.Any())
        {
            PersonId = teilnehmer[0].PersonId;
            idx = 0;
        }

        if (idx >= 0)
        {
            TeilnehmerNr      = idx + 1;
            PersonName        = teilnehmer[idx].Name;
            VorherigePersonId = idx > 0 ? teilnehmer[idx - 1].PersonId : 0;
            NaechstePersonId  = idx < teilnehmer.Count - 1 ? teilnehmer[idx + 1].PersonId : 0;
        }

        // Fächer + Noten
        var faecher = await _db.MeisterFaecher
            .Where(f => f.LehrgangId == LehrgangId)
            .OrderBy(f => f.Reihenfolge)
            .ToListAsync();

        var vorhandeneNoten = await _db.MeisterNoten
            .Where(n => n.LehrgangId == LehrgangId && n.PersonId == PersonId)
            .ToListAsync();

        FachNoten = faecher.Select(f => {
            var note = vorhandeneNoten.FirstOrDefault(n => n.FachId == f.FachId);
            return new NotenFachZeile(
                f.FachId, f.Bezeichnung, f.Gewichtung,
                note?.NoteId ?? 0, note?.Note, note?.BewertendeDozentPersonId, note?.BewertungsDatum,
                note?.Notiz);
        }).ToList();

        GesamtFaecher   = faecher.Count;
        BenoteteFaecher = FachNoten.Count(fn => fn.Note.HasValue);

        // Gesamtnote berechnen
        var benotet = FachNoten.Where(fn => fn.Note.HasValue).ToList();
        if (benotet.Count == GesamtFaecher && GesamtFaecher > 0)
        {
            var fachGew = faecher.ToDictionary(f => f.FachId, f => f.Gewichtung);
            var sumGew  = faecher.Sum(f => f.Gewichtung);
            if (sumGew > 0)
                Gesamtnote = Math.Round(benotet.Sum(fn => (decimal)fn.Note!.Value * fachGew[fn.FachId]) / sumGew, 1);
        }

        // Dozenten
        var rawDozenten = await _db.PersonRollen
            .Where(r => r.Status == 0 && r.RolleTyp == PersonRolleTyp.Dozent)
            .Select(r => new DozentenAuswahl(r.Person.PersonId, r.Person.PersonNr,
                r.Person.Nachname + ", " + r.Person.Vorname))
            .ToListAsync();
        DozentenListe = rawDozenten.DistinctBy(d => d.PersonId).OrderBy(d => d.AnzeigeName).ToList();
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

public record NotenFachZeile(
    int FachId, string Bezeichnung, decimal Gewichtung,
    int NoteId, byte? Note, int? DozentPersonId, DateOnly? BewertungsDatum, string? Notiz);
