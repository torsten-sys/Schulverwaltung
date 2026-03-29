using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class MeisterNotenFachModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public MeisterNotenFachModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int  LehrgangId { get; set; }
    [BindProperty(SupportsGet = true)] public int? FachId     { get; set; }

    public Lehrgang?                Lehrgang         { get; set; }
    public MeisterFach?             AktuellesFach    { get; set; }
    public List<MeisterFach>        AlleFaecher       { get; set; } = [];
    public List<TeilnehmerNotenZeile> Teilnehmer     { get; set; } = [];
    public List<DozentenAuswahl>    DozentenListe     { get; set; } = [];
    public int                      FachNr            { get; set; }
    public int                      FachGesamt        { get; set; }
    public int                      VorherigesFachId  { get; set; }
    public int                      NaechstesFachId   { get; set; }
    public decimal?                 DurchschnittsNote { get; set; }
    public int                      BenoteteTeilnehmer { get; set; }
    public int                      GesamtTeilnehmer  { get; set; }
    public string?                  Fehler            { get; set; }

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
        [FromForm] List<int>     personIds,
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

        var fach = FachId.HasValue ? await _db.MeisterFaecher.FindAsync(FachId.Value) : null;
        if (fach == null) return NotFound();

        var belegNr = await NextNoAsync("AENDERUNG");
        var user    = User.Identity?.Name ?? "System";

        // Alle Teilnehmer-Personen für Snapshots
        var teilnehmerPersonen = await _db.LehrgangPersonen
            .Include(lp => lp.Person)
            .Where(lp => lp.LehrgangId == LehrgangId && lp.Rolle == LehrgangPersonRolle.Teilnehmer && lp.Status != 2)
            .ToDictionaryAsync(lp => lp.PersonId, lp => lp.Person);

        for (int i = 0; i < personIds.Count; i++)
        {
            var personId  = personIds[i];
            var noteStr   = i < noten.Count ? noten[i] : "";
            var noteVal   = byte.TryParse(noteStr, out var nb) ? (byte?)nb : null;
            var dozentId  = i < dozentPersonIds.Count ? dozentPersonIds[i] : null;
            var datumStr  = i < bewertungsDaten.Count ? bewertungsDaten[i] : "";
            var notiz     = i < notizen.Count ? notizen[i] : "";
            var noteId    = i < noteIds.Count ? noteIds[i] : 0;

            DateOnly? bewDatum = DateOnly.TryParse(datumStr, out var bd) ? bd : null;

            MeisterNote? vorhandenNote = noteId > 0
                ? await _db.MeisterNoten.AsNoTracking().FirstOrDefaultAsync(n => n.NoteId == noteId)
                : await _db.MeisterNoten.AsNoTracking()
                    .FirstOrDefaultAsync(n => n.LehrgangId == LehrgangId && n.FachId == FachId!.Value && n.PersonId == personId);

            string? dozentName = null;
            if (dozentId.HasValue)
            {
                var doz = await _db.Personen.FindAsync(dozentId.Value);
                dozentName = doz != null ? $"{doz.Nachname}, {doz.Vorname}" : null;
            }

            if (vorhandenNote == null)
            {
                if (noteVal == null) continue;
                if (!teilnehmerPersonen.TryGetValue(personId, out var person)) continue;

                var neu = new MeisterNote {
                    LehrgangId               = LehrgangId,
                    FachId                   = FachId!.Value,
                    PersonId                 = personId,
                    PersonNr                 = person.PersonNr,
                    PersonName               = person.Nachname + ", " + person.Vorname,
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
                    FachBezeichnung      = fach.Bezeichnung,
                    PersonNr             = person.PersonNr,
                    PersonName           = person.Nachname + ", " + person.Vorname,
                    AlteNote             = null,
                    NeueNote             = noteVal.Value,
                    BewertendeDozentName = dozentName,
                    Zeitstempel          = DateTime.UtcNow,
                    AusfuehrendUser      = user
                });
            }
            else
            {
                var alteNote  = vorhandenNote.Note;
                var geaendert = alteNote != noteVal
                    || vorhandenNote.BewertendeDozentPersonId != dozentId
                    || vorhandenNote.BewertungsDatum != bewDatum
                    || vorhandenNote.Notiz != (string.IsNullOrWhiteSpace(notiz) ? null : notiz);

                if (geaendert)
                {
                    var dbNote = await _db.MeisterNoten.FindAsync(vorhandenNote.NoteId);
                    if (dbNote == null) continue;

                    dbNote.Note                     = noteVal;
                    dbNote.BewertendeDozentPersonId = dozentId;
                    dbNote.BewertendeDozentName     = dozentName;
                    dbNote.BewertungsDatum          = bewDatum;
                    dbNote.Notiz                    = string.IsNullOrWhiteSpace(notiz) ? null : notiz;

                    if (alteNote != noteVal && noteVal.HasValue)
                    {
                        if (!teilnehmerPersonen.TryGetValue(personId, out var person)) continue;
                        _db.MeisterNoteAenderungsposten.Add(new MeisterNoteAenderungsposten {
                            BelegNr              = belegNr,
                            NoteId               = dbNote.NoteId,
                            LehrgangId           = LehrgangId,
                            LehrgangNr           = lehrgang.LehrgangNr,
                            FachBezeichnung      = fach.Bezeichnung,
                            PersonNr             = person.PersonNr,
                            PersonName           = person.Nachname + ", " + person.Vorname,
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
        return RedirectToPage(new { lehrgangId = LehrgangId, fachId = FachId });
    }

    private async Task LadeDaten()
    {
        Lehrgang = await _db.Lehrgaenge.FindAsync(LehrgangId);
        if (Lehrgang == null) return;

        AlleFaecher = await _db.MeisterFaecher
            .Where(f => f.LehrgangId == LehrgangId)
            .OrderBy(f => f.Reihenfolge)
            .ToListAsync();

        FachGesamt = AlleFaecher.Count;

        if (!FachId.HasValue && AlleFaecher.Any())
            FachId = AlleFaecher.First().FachId;

        if (FachId.HasValue)
        {
            AktuellesFach = AlleFaecher.FirstOrDefault(f => f.FachId == FachId.Value);
            var idx = AlleFaecher.FindIndex(f => f.FachId == FachId.Value);
            if (idx >= 0)
            {
                FachNr           = idx + 1;
                VorherigesFachId = idx > 0 ? AlleFaecher[idx - 1].FachId : 0;
                NaechstesFachId  = idx < AlleFaecher.Count - 1 ? AlleFaecher[idx + 1].FachId : 0;
            }

            // Teilnehmer laden
            var teilnehmer = await _db.LehrgangPersonen
                .Include(lp => lp.Person)
                .Where(lp => lp.LehrgangId == LehrgangId && lp.Rolle == LehrgangPersonRolle.Teilnehmer && lp.Status != 2)
                .OrderBy(lp => lp.Person.Nachname).ThenBy(lp => lp.Person.Vorname)
                .ToListAsync();

            var noten = await _db.MeisterNoten
                .Where(n => n.LehrgangId == LehrgangId && n.FachId == FachId.Value)
                .ToListAsync();

            GesamtTeilnehmer = teilnehmer.Count;

            Teilnehmer = teilnehmer.Select(tn => {
                var note = noten.FirstOrDefault(n => n.PersonId == tn.PersonId);
                return new TeilnehmerNotenZeile(
                    tn.PersonId,
                    tn.Person.PersonNr,
                    tn.Person.Nachname + ", " + tn.Person.Vorname,
                    note?.NoteId ?? 0,
                    note?.Note,
                    note?.BewertendeDozentPersonId,
                    note?.BewertungsDatum,
                    note?.Notiz);
            }).ToList();

            BenoteteTeilnehmer = Teilnehmer.Count(t => t.Note.HasValue);

            var benotet = Teilnehmer.Where(t => t.Note.HasValue).ToList();
            if (benotet.Any())
                DurchschnittsNote = Math.Round((decimal)benotet.Sum(t => (int)t.Note!.Value) / benotet.Count, 1);
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

public record TeilnehmerNotenZeile(
    int PersonId, string PersonNr, string PersonName,
    int NoteId, byte? Note, int? DozentPersonId, DateOnly? BewertungsDatum, string? Notiz);
