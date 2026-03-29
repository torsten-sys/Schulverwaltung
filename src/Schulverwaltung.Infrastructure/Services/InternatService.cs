using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Infrastructure.Services;

/// <summary>
/// Service für Internat-Geschäftslogik (Zimmerverwaltung, Belegungen, Protokoll).
/// </summary>
public class InternatService
{
    private readonly SchulverwaltungDbContext _db;
    public InternatService(SchulverwaltungDbContext db) => _db = db;

    // ── Verfügbarkeitsprüfung ─────────────────────────────────────────────────

    /// <summary>
    /// Prüft ob ein Raum (Internat) im Zeitraum noch freie Kapazität hat.
    /// Gesperrte Räume sind nie verfügbar.
    /// </summary>
    public async Task<bool> IstZimmerVerfuegbarAsync(
        int raumId, DateOnly von, DateOnly bis, int? ausgenommenBelegungId = null)
    {
        var raum = await _db.Raeume.FindAsync(raumId);
        if (raum == null || raum.Gesperrt) return false;

        var query = _db.InternatBelegungen
            .Where(b => b.RaumId == raumId
                     && b.Von <= bis
                     && b.Bis >= von);

        if (ausgenommenBelegungId.HasValue)
            query = query.Where(b => b.BelegungId != ausgenommenBelegungId.Value);

        int anzahl = await query.CountAsync();
        return anzahl < (raum.Kapazitaet ?? 1);
    }

    /// <summary>
    /// Alle Internat-Räume die im Zeitraum noch freie Kapazität haben.
    /// </summary>
    public async Task<List<Raum>> GetVerfuegbareZimmerAsync(DateOnly von, DateOnly bis)
    {
        var raeume = await _db.Raeume
            .Include(r => r.RaumTyp)
            .Where(r => !r.Gesperrt && r.RaumTyp.IstInternat)
            .OrderBy(r => r.RaumNr)
            .ToListAsync();

        var belegungen = await _db.InternatBelegungen
            .Where(b => b.Von <= bis && b.Bis >= von)
            .GroupBy(b => b.RaumId)
            .Select(g => new { RaumId = g.Key, Anzahl = g.Count() })
            .ToListAsync();

        var belegungsDict = belegungen
            .Where(x => x.RaumId.HasValue)
            .ToDictionary(x => x.RaumId!.Value, x => x.Anzahl);

        return raeume
            .Where(r => (belegungsDict.TryGetValue(r.RaumId, out var anz) ? anz : 0) < (r.Kapazitaet ?? 1))
            .ToList();
    }

    // ── Belegung erstellen ────────────────────────────────────────────────────

    public async Task<InternatBelegung> BelegungErstellenAsync(
        InternatBelegung belegung, string user)
    {
        // Verfügbarkeit prüfen
        if (belegung.RaumId.HasValue &&
            !await IstZimmerVerfuegbarAsync(belegung.RaumId.Value, belegung.Von, belegung.Bis))
            throw new InvalidOperationException(
                "Das Zimmer ist im gewählten Zeitraum bereits voll belegt oder gesperrt.");

        // Snapshot-Felder befüllen
        var person = await _db.Personen.FindAsync(belegung.PersonId);
        if (person == null)
            throw new InvalidOperationException("Person nicht gefunden.");

        belegung.PersonNr   = person.PersonNr;
        belegung.PersonName = person.Nachname + ", " + person.Vorname;
        belegung.CreatedBy  = user;

        if (belegung.LehrgangId.HasValue)
        {
            var lehrgang = await _db.Lehrgaenge.FindAsync(belegung.LehrgangId.Value);
            if (lehrgang != null)
            {
                belegung.LehrgangNr          = lehrgang.LehrgangNr;
                belegung.LehrgangBezeichnung = lehrgang.Bezeichnung;
            }
        }

        _db.InternatBelegungen.Add(belegung);
        await _db.SaveChangesAsync();

        // Aenderungsposten schreiben
        var raum    = belegung.RaumId.HasValue ? await _db.Raeume.FindAsync(belegung.RaumId.Value) : null;
        var belegNr = await NextNoAsync("AENDERUNG");
        _db.InternatAenderungsposten.Add(new InternatAenderungsposten
        {
            BelegNr           = belegNr,
            ZimmerId          = belegung.RaumId ?? 0,
            ZimmerNr          = raum?.RaumNr ?? "",
            ZimmerBezeichnung = raum?.Bezeichnung ?? "",
            BelegungId        = belegung.BelegungId,
            PersonNr          = belegung.PersonNr,
            PersonName        = belegung.PersonName,
            Ereignis          = "BelegungErstellt",
            Tabelle           = "InternatBelegung",
            NeuerWert         = $"{belegung.Von:dd.MM.yyyy} – {belegung.Bis:dd.MM.yyyy}",
            AusfuehrendUser   = user
        });
        await _db.SaveChangesAsync();

        return belegung;
    }

    // ── Belegung aktualisieren ────────────────────────────────────────────────

    public async Task BelegungAktualisierenAsync(
        int belegungId, DateOnly von, DateOnly bis,
        byte belegungsTyp, byte kostenArt, decimal? kosten, string? notiz, string user)
    {
        var b = await _db.InternatBelegungen.FindAsync(belegungId)
            ?? throw new InvalidOperationException("Belegung nicht gefunden.");

        if (b.RaumId.HasValue &&
            !await IstZimmerVerfuegbarAsync(b.RaumId.Value, von, bis, belegungId))
            throw new InvalidOperationException(
                "Das Zimmer ist im gewählten Zeitraum bereits voll belegt.");

        var raum = b.RaumId.HasValue ? await _db.Raeume.FindAsync(b.RaumId.Value) : null;

        var belegNr = await NextNoAsync("AENDERUNG");
        _db.InternatAenderungsposten.Add(new InternatAenderungsposten
        {
            BelegNr           = belegNr,
            ZimmerId          = b.RaumId ?? 0,
            ZimmerNr          = raum?.RaumNr ?? "",
            ZimmerBezeichnung = raum?.Bezeichnung ?? "",
            BelegungId        = belegungId,
            PersonNr          = b.PersonNr,
            PersonName        = b.PersonName,
            Ereignis          = "BelegungGeaendert",
            Tabelle           = "InternatBelegung",
            AlterWert         = $"{b.Von:dd.MM.yyyy} – {b.Bis:dd.MM.yyyy}",
            NeuerWert         = $"{von:dd.MM.yyyy} – {bis:dd.MM.yyyy}",
            AusfuehrendUser   = user
        });

        b.Von          = von;
        b.Bis          = bis;
        b.BelegungsTyp = belegungsTyp;
        b.KostenArt    = kostenArt;
        b.Kosten       = kosten;
        b.Notiz        = notiz;

        await _db.SaveChangesAsync();
    }

    // ── Belegung stornieren ───────────────────────────────────────────────────

    public async Task BelegungStornierenAsync(int belegungId, string user)
    {
        var b = await _db.InternatBelegungen.FindAsync(belegungId)
            ?? throw new InvalidOperationException("Belegung nicht gefunden.");

        var raum = b.RaumId.HasValue ? await _db.Raeume.FindAsync(b.RaumId.Value) : null;

        // Aenderungsposten VOR dem Löschen schreiben
        var belegNr = await NextNoAsync("AENDERUNG");
        _db.InternatAenderungsposten.Add(new InternatAenderungsposten
        {
            BelegNr           = belegNr,
            ZimmerId          = b.RaumId ?? 0,
            ZimmerNr          = raum?.RaumNr ?? "",
            ZimmerBezeichnung = raum?.Bezeichnung ?? "",
            BelegungId        = belegungId,
            PersonNr          = b.PersonNr,
            PersonName        = b.PersonName,
            Ereignis          = "BelegungStorniert",
            Tabelle           = "InternatBelegung",
            AlterWert         = $"{b.Von:dd.MM.yyyy} – {b.Bis:dd.MM.yyyy}",
            AusfuehrendUser   = user
        });
        await _db.SaveChangesAsync();

        _db.InternatBelegungen.Remove(b);
        await _db.SaveChangesAsync();
    }

    // ── Bezahlt markieren ─────────────────────────────────────────────────────

    public async Task BezahltMarkierenAsync(int belegungId, string user)
    {
        var b = await _db.InternatBelegungen.FindAsync(belegungId)
            ?? throw new InvalidOperationException("Belegung nicht gefunden.");

        var raum = b.RaumId.HasValue ? await _db.Raeume.FindAsync(b.RaumId.Value) : null;

        b.Bezahlt   = true;
        b.BezahltAm = DateOnly.FromDateTime(DateTime.Today);

        var belegNr = await NextNoAsync("AENDERUNG");
        _db.InternatAenderungsposten.Add(new InternatAenderungsposten
        {
            BelegNr           = belegNr,
            ZimmerId          = b.RaumId ?? 0,
            ZimmerNr          = raum?.RaumNr ?? "",
            ZimmerBezeichnung = raum?.Bezeichnung ?? "",
            BelegungId        = belegungId,
            PersonNr          = b.PersonNr,
            PersonName        = b.PersonName,
            Ereignis          = "BezahltMarkiert",
            Tabelle           = "InternatBelegung",
            NeuerWert         = b.BezahltAm?.ToString("dd.MM.yyyy"),
            AusfuehrendUser   = user
        });

        await _db.SaveChangesAsync();
    }

    // ── Zimmer sperren / entsperren ───────────────────────────────────────────

    public async Task ZimmerSperrenAsync(int raumId, string sperrGrund, string user)
    {
        var r = await _db.Raeume.FindAsync(raumId)
            ?? throw new InvalidOperationException("Zimmer nicht gefunden.");

        r.Gesperrt   = true;
        r.SperrGrund = sperrGrund;

        var belegNr = await NextNoAsync("AENDERUNG");
        _db.InternatAenderungsposten.Add(new InternatAenderungsposten
        {
            BelegNr           = belegNr,
            ZimmerId          = raumId,
            ZimmerNr          = r.RaumNr,
            ZimmerBezeichnung = r.Bezeichnung,
            Ereignis          = "ZimmerGesperrt",
            Tabelle           = "Raum",
            Feld              = "SperrGrund",
            NeuerWert         = sperrGrund,
            AusfuehrendUser   = user
        });
        await _db.SaveChangesAsync();
    }

    public async Task ZimmerEntsperrenAsync(int raumId, string user)
    {
        var r = await _db.Raeume.FindAsync(raumId)
            ?? throw new InvalidOperationException("Zimmer nicht gefunden.");

        r.Gesperrt   = false;
        r.SperrGrund = null;

        var belegNr = await NextNoAsync("AENDERUNG");
        _db.InternatAenderungsposten.Add(new InternatAenderungsposten
        {
            BelegNr           = belegNr,
            ZimmerId          = raumId,
            ZimmerNr          = r.RaumNr,
            ZimmerBezeichnung = r.Bezeichnung,
            Ereignis          = "ZimmerEntsperrt",
            Tabelle           = "Raum",
            AusfuehrendUser   = user
        });
        await _db.SaveChangesAsync();
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    public static string BelegungsTypText(byte typ) => typ switch
    {
        0 => "Meisterkurs",
        1 => "Sonderlehrgang",
        2 => "Dozent",
        _ => ""
    };

    public static string BelegungsTypFarbe(byte typ) => typ switch
    {
        0 => "#1F3864",
        1 => "#00B7C3",
        2 => "#107C10",
        _ => "#a19f9d"
    };

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
