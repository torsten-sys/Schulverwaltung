using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Infrastructure.Services;

/// <summary>
/// Service für Meisterkurs-spezifische Geschäftslogik.
/// </summary>
public class MeisterkursService
{
    private readonly SchulverwaltungDbContext _db;
    public MeisterkursService(SchulverwaltungDbContext db) => _db = db;

    // ─────────────────────────────────────────────────────────────────────────
    // Standardbezeichnungen
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly (int Nr, string Bez, byte Typ)[] Abschnitte =
    {
        (1,  "Eigenversorgung Einlagen",          0),
        (2,  "Eigenversorgung Maßschuhe",         0),
        (3,  "Arbeitsprobe Lähmungsversorgung",   0),
        (4,  "Arbeitsprobe Arthrodesenversorgung",0),
        (5,  "Medizinische Fußpflege",            0),
        (6,  "1. Patientenversorgung",            1),
        (7,  "2. Patientenversorgung",            1),
        (8,  "3. Patientenversorgung",            1),
        (9,  "4. Patientenversorgung",            1),
        (10, "Meisterprüfung",                    2),
    };

    private static readonly string[] Faecher =
    {
        "Befunderhebung", "Gipstechnik", "Leistenbau", "Fußbettung",
        "Schaftbau", "Bodenbau", "Innenschuh", "Einlagen", "Biomechanik",
        "Betriebs- & Materialkunde", "Fachtechnologie", "Fachkalkulation",
        "Fachzeichnen", "Anatomie", "Physiologie", "Orthopädie",
        "Medizinische Fußpflege"
    };

    private static readonly string[] FunktionsNamen =
    {
        "Gruppensprecher", "Klassenbuchwart", "Postwart", "Lagerwart",
        "Bandsägenraumwart", "Kunststoffwerkstattwart", "Maß- und Gipsraumwart",
        "Schaftraumwart", "Werkstattwart", "Internatswart"
    };

    public static string FunktionName(byte f) => f < FunktionsNamen.Length ? FunktionsNamen[f] : f.ToString();

    // ─────────────────────────────────────────────────────────────────────────
    // AbschnittInitialisieren – erstellt alle 10 Abschnitte, 17 Fächer
    // sowie leere MeisterNote-Zeilen für alle Teilnehmer des Lehrgangs.
    // ─────────────────────────────────────────────────────────────────────────
    public async Task AbschnittInitialisierenAsync(int lehrgangId, string user)
    {
        // Abschnitte
        foreach (var (nr, bez, typ) in Abschnitte)
        {
            _db.MeisterAbschnitte.Add(new MeisterAbschnitt
            {
                LehrgangId   = lehrgangId,
                Nummer       = nr,
                Bezeichnung  = bez,
                AbschnittTyp = typ,
                Reihenfolge  = nr
            });
        }

        // Fächer
        var fachEntities = new List<MeisterFach>();
        for (int i = 0; i < Faecher.Length; i++)
        {
            var fach = new MeisterFach
            {
                LehrgangId  = lehrgangId,
                Bezeichnung = Faecher[i],
                Gewichtung  = 1.0m,
                Reihenfolge = i + 1
            };
            _db.MeisterFaecher.Add(fach);
            fachEntities.Add(fach);
        }

        await _db.SaveChangesAsync(); // IDs für Fächer benötigt

        // Leere Notenzeilen für alle aktuellen Teilnehmer
        var teilnehmer = await _db.LehrgangPersonen
            .Where(lp => lp.LehrgangId == lehrgangId && lp.Rolle == LehrgangPersonRolle.Teilnehmer)
            .Include(lp => lp.Person)
            .ToListAsync();

        foreach (var lp in teilnehmer)
            NoteZeilenFuerTeilnehmerAnlegen(lehrgangId, lp, fachEntities, user);

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Für einen neu hinzugefügten Teilnehmer leere Notenzeilen für alle
    /// bestehenden Fächer anlegen. Wird aus Karte.cshtml.cs aufgerufen.
    /// </summary>
    public async Task NoteZeilenFuerNeuenTeilnehmerAsync(int lehrgangId, int personId, string user)
    {
        var person = await _db.Personen.FindAsync(personId);
        if (person == null) return;

        var faecher = await _db.MeisterFaecher
            .Where(f => f.LehrgangId == lehrgangId)
            .ToListAsync();

        foreach (var fach in faecher)
        {
            var exists = await _db.MeisterNoten.AnyAsync(n =>
                n.LehrgangId == lehrgangId && n.FachId == fach.FachId && n.PersonId == personId);
            if (exists) continue;

            _db.MeisterNoten.Add(new MeisterNote
            {
                LehrgangId  = lehrgangId,
                FachId      = fach.FachId,
                PersonId    = personId,
                PersonNr    = person.PersonNr,
                PersonName  = person.Nachname + ", " + person.Vorname,
                CreatedBy   = user
            });
        }
        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GesamtnoteBerechnen
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<decimal?> GesamtnoteBerechnenAsync(int lehrgangId, int personId)
    {
        var anzahlFaecher = await _db.MeisterFaecher.CountAsync(f => f.LehrgangId == lehrgangId);
        if (anzahlFaecher == 0) return null;

        var noten = await _db.MeisterNoten
            .Where(n => n.LehrgangId == lehrgangId && n.PersonId == personId)
            .Include(n => n.Fach)
            .ToListAsync();

        var bewertet = noten.Where(n => n.Note.HasValue).ToList();
        if (bewertet.Count < anzahlFaecher) return null;  // noch nicht alle bewertet

        var summeGewichtet  = bewertet.Sum(n => (decimal)n.Note!.Value * n.Fach.Gewichtung);
        var summeGewichtung = bewertet.Sum(n => n.Fach.Gewichtung);
        if (summeGewichtung == 0) return null;

        return Math.Round(summeGewichtet / summeGewichtung, 1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NoteSetzen
    // ─────────────────────────────────────────────────────────────────────────
    public async Task NoteSetzenAsync(int noteId, byte note, int? dozentPersonId, string user)
    {
        var n = await _db.MeisterNoten
            .Include(x => x.Fach)
            .FirstOrDefaultAsync(x => x.NoteId == noteId)
            ?? throw new InvalidOperationException($"Note {noteId} nicht gefunden.");

        var lgNr = await _db.Lehrgaenge
            .Where(l => l.LehrgangId == n.LehrgangId)
            .Select(l => l.LehrgangNr)
            .FirstOrDefaultAsync() ?? "";

        string? dozentName = null;
        if (dozentPersonId.HasValue)
        {
            var dozent = await _db.Personen.FindAsync(dozentPersonId.Value);
            if (dozent != null) dozentName = dozent.Nachname + ", " + dozent.Vorname;
        }

        var belegNr = await NextNoAsync("AENDERUNG");
        _db.MeisterNoteAenderungsposten.Add(new MeisterNoteAenderungsposten
        {
            BelegNr              = belegNr,
            NoteId               = noteId,
            LehrgangId           = n.LehrgangId,
            LehrgangNr           = lgNr,
            FachBezeichnung      = n.Fach.Bezeichnung,
            PersonNr             = n.PersonNr,
            PersonName           = n.PersonName,
            AlteNote             = n.Note,
            NeueNote             = note,
            BewertendeDozentName = dozentName,
            Zeitstempel          = DateTime.UtcNow,
            AusfuehrendUser      = user
        });

        n.Note                     = note;
        n.BewertendeDozentPersonId = dozentPersonId;
        n.BewertendeDozentName     = dozentName;
        n.BewertungsDatum          = DateOnly.FromDateTime(DateTime.Today);

        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ZuordnungBestaetigen  (Planung → Bestätigt)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task ZuordnungBestaetigenAsync(int zuordnungId, string user)
    {
        var z = await _db.MeisterPatientenZuordnungen.FindAsync(zuordnungId)
            ?? throw new InvalidOperationException("Zuordnung nicht gefunden.");
        if (z.BuchungsStatus != 0)
            throw new InvalidOperationException("Zuordnung ist nicht im Status Planung.");

        z.BuchungsStatus = 1;
        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ZuordnungBuchen  (Bestätigt → Gebucht + Buchungsposten schreiben)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task ZuordnungBuchenAsync(int zuordnungId, string user)
    {
        var z = await _db.MeisterPatientenZuordnungen
            .Include(x => x.Termine)
            .Include(x => x.Abschnitt)
            .FirstOrDefaultAsync(x => x.ZuordnungId == zuordnungId)
            ?? throw new InvalidOperationException("Zuordnung nicht gefunden.");

        if (z.BuchungsStatus != 1)
            throw new InvalidOperationException("Zuordnung muss im Status Bestätigt sein.");

        var t1 = z.Termine.FirstOrDefault(t => t.TerminTyp == 0);
        var t2 = z.Termine.FirstOrDefault(t => t.TerminTyp == 1);
        var t3 = z.Termine.FirstOrDefault(t => t.TerminTyp == 2);

        if (t1 == null || t2 == null || t3 == null)
            throw new InvalidOperationException("Alle 3 Termine müssen vorhanden sein.");
        if (t3.Status != 2)
            throw new InvalidOperationException("Termin Auslieferung muss den Status 'Durchgeführt' haben.");

        var lgNr = await _db.Lehrgaenge
            .Where(l => l.LehrgangId == z.LehrgangId)
            .Select(l => l.LehrgangNr)
            .FirstOrDefaultAsync() ?? "";

        var belegNr = await NextNoAsync("MEISTERBUCHUNG");
        _db.MeisterPatientenBuchungsposten.Add(new MeisterPatientenBuchungsposten
        {
            BelegNr              = belegNr,
            LehrgangId           = z.LehrgangId,
            LehrgangNr           = lgNr,
            AbschnittNummer      = z.Abschnitt.Nummer,
            AbschnittBezeichnung = z.Abschnitt.Bezeichnung,
            BuchungsDatum        = DateTime.UtcNow,
            PatientPersonId      = z.PatientPersonId,
            PatientNr            = z.PatientPersonNr,
            PatientName          = z.PatientName,
            Meisterschueler1PersonId = z.Meisterschueler1PersonId,
            MS1Nr                = z.Meisterschueler1Nr,
            MS1Name              = z.Meisterschueler1Name,
            Meisterschueler2PersonId = z.Meisterschueler2PersonId,
            MS2Nr                = z.Meisterschueler2Nr,
            MS2Name              = z.Meisterschueler2Name,
            IstErsatzpatient               = z.IstErsatzpatient,
            PruefungskommissionZugelassen  = z.PruefungskommissionZugelassen,
            Termin1Datum         = t1.Datum,
            Termin1Status        = t1.Status,
            Termin2Datum         = t2.Datum,
            Termin2Status        = t2.Status,
            Termin3Datum         = t3.Datum,
            Termin3Status        = t3.Status,
            HilfsmittelUebergeben  = t3.HilfsmittelUebergeben,
            NichtUebergebenGrund   = t3.NichtUebergebenGrund,
            GebuchtvonUser         = user,
            GebuchtAm              = DateTime.UtcNow
        });

        z.BuchungsStatus = 2;
        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ZeugnisGenerieren – Daten für Zeugnis zusammenstellen
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<MeisterkursZeugnisData?> ZeugnisGenerierenAsync(int lehrgangId, int personId)
    {
        var lehrgang = await _db.Lehrgaenge.FindAsync(lehrgangId);
        if (lehrgang == null) return null;

        var faecher = await _db.MeisterFaecher
            .Where(f => f.LehrgangId == lehrgangId)
            .OrderBy(f => f.Reihenfolge)
            .ToListAsync();

        var noten = await _db.MeisterNoten
            .Where(n => n.LehrgangId == lehrgangId && n.PersonId == personId)
            .ToListAsync();

        if (noten.Any(n => !n.Note.HasValue)) return null; // nicht alle bewertet

        var person = noten.FirstOrDefault();
        if (person == null) return null;

        var gesamtnote = await GesamtnoteBerechnenAsync(lehrgangId, personId);

        var notenListe = faecher.Select(f =>
        {
            var n = noten.FirstOrDefault(x => x.FachId == f.FachId);
            return new MeisterkursZeugnisNote(f.Bezeichnung, n?.Note, f.Gewichtung);
        }).ToList();

        return new MeisterkursZeugnisData(
            PersonNr:     person.PersonNr,
            PersonName:   person.PersonName,
            LehrgangNr:   lehrgang.LehrgangNr,
            LehrgangName: lehrgang.Bezeichnung,
            StartDatum:   lehrgang.StartDatum,
            EndDatum:     lehrgang.EndDatum,
            Noten:        notenListe,
            Gesamtnote:   gesamtnote,
            GesamtnoteText: gesamtnote.HasValue ? NotenText(gesamtnote.Value) : null
        );
    }

    private static string NotenText(decimal note) => note switch
    {
        <= 1.5m => "sehr gut",
        <= 2.5m => "gut",
        <= 3.5m => "befriedigend",
        <= 4.5m => "ausreichend",
        <= 5.5m => "mangelhaft",
        _       => "ungenügend"
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Hilfsmethoden
    // ─────────────────────────────────────────────────────────────────────────

    private void NoteZeilenFuerTeilnehmerAnlegen(
        int lehrgangId, LehrgangPerson lp, List<MeisterFach> faecher, string user)
    {
        foreach (var fach in faecher)
        {
            _db.MeisterNoten.Add(new MeisterNote
            {
                LehrgangId  = lehrgangId,
                FachId      = fach.FachId,
                PersonId    = lp.PersonId,
                PersonNr    = lp.Person.PersonNr,
                PersonName  = lp.Person.Nachname + ", " + lp.Person.Vorname,
                CreatedBy   = user
            });
        }
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

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

public record MeisterkursZeugnisNote(string Fach, byte? Note, decimal Gewichtung);

public record MeisterkursZeugnisData(
    string PersonNr, string PersonName,
    string LehrgangNr, string LehrgangName,
    DateOnly StartDatum, DateOnly? EndDatum,
    List<MeisterkursZeugnisNote> Noten,
    decimal? Gesamtnote, string? GesamtnoteText);
