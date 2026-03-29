using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Infrastructure.Services;

public class InventarService
{
    private readonly SchulverwaltungDbContext _db;
    public InventarService(SchulverwaltungDbContext db) => _db = db;

    // ── Zustand-Text ──────────────────────────────────────────────────────────
    public static string ZustandText(byte z) => z switch {
        0 => "Gut",
        1 => "Beschädigt",
        2 => "Defekt",
        3 => "Ausgemustert",
        _ => "Unbekannt"
    };

    // ── Nächste Wartung berechnen ─────────────────────────────────────────────
    public static DateOnly? NaechsteWartungBerechnen(Inventar inv)
    {
        if (!inv.WartungIntervallMonate.HasValue) return null;
        var basis = inv.WartungLetztesDatum ?? inv.WartungStartdatum;
        if (!basis.HasValue) return null;
        return basis.Value.AddMonths(inv.WartungIntervallMonate.Value);
    }

    // ── Inventar anlegen ─────────────────────────────────────────────────────
    public async Task InventarAnlegenAsync(Inventar inventar, string ausfuehrendUser)
    {
        inventar.InventarNr = await NextNoAsync("INVENTAR");
        _db.Inventar.Add(inventar);
        await _db.SaveChangesAsync();

        var belegNr = await NextNoAsync("AENDERUNG");
        _db.InventarAenderungsposten.Add(new InventarAenderungsposten {
            BelegNr         = belegNr,
            InventarId      = inventar.InventarId,
            InventarNr      = inventar.InventarNr,
            Bezeichnung     = inventar.Bezeichnung,
            Ereignis        = "Erstellt",
            Zeitstempel     = DateTime.UtcNow,
            AusfuehrendUser = ausfuehrendUser
        });
        await _db.SaveChangesAsync();
    }

    // ── Raum anlegen ─────────────────────────────────────────────────────────
    public async Task RaumAnlegenAsync(Raum raum)
    {
        raum.RaumNr = await NextNoAsync("RAUM");
        _db.Raeume.Add(raum);
        await _db.SaveChangesAsync();
    }

    // ── Aenderungsposten schreiben ────────────────────────────────────────────
    public async Task AenderungspostenAsync(int inventarId, string inventarNr, string bezeichnung,
        string ereignis, string? feld, string? alterWert, string? neuerWert, string ausfuehrendUser)
    {
        var belegNr = await NextNoAsync("AENDERUNG");
        _db.InventarAenderungsposten.Add(new InventarAenderungsposten {
            BelegNr         = belegNr,
            InventarId      = inventarId,
            InventarNr      = inventarNr,
            Bezeichnung     = bezeichnung,
            Ereignis        = ereignis,
            Feld            = feld,
            AlterWert       = alterWert,
            NeuerWert       = neuerWert,
            Zeitstempel     = DateTime.UtcNow,
            AusfuehrendUser = ausfuehrendUser
        });
        await _db.SaveChangesAsync();
    }

    // ── Wartung durchführen ───────────────────────────────────────────────────
    public async Task WartungDurchfuehrenAsync(int inventarId, InventarWartung wartung, string ausfuehrendUser)
    {
        // Snapshot BetriebName
        if (wartung.IstExtern && wartung.BetriebId.HasValue)
        {
            var betrieb = await _db.Betriebe.FindAsync(wartung.BetriebId.Value);
            wartung.BetriebName = betrieb?.Name;
        }
        wartung.InventarId      = inventarId;
        wartung.AusfuehrendUser = ausfuehrendUser;
        wartung.ErstelltAm      = DateTime.UtcNow;
        _db.InventarWartungen.Add(wartung);

        var inv = await _db.Inventar.FindAsync(inventarId)
            ?? throw new InvalidOperationException("Inventar nicht gefunden.");
        inv.WartungLetztesDatum = wartung.WartungsDatum;
        if (inv.WartungIntervallMonate.HasValue)
            inv.WartungNaechstesDatum = wartung.WartungsDatum.AddMonths(inv.WartungIntervallMonate.Value);

        await _db.SaveChangesAsync();

        var neuerWert = $"Datum: {wartung.WartungsDatum:dd.MM.yyyy}, {(wartung.IstExtern ? "Extern" : "Intern")}" +
            (wartung.BetriebName != null ? $", Betrieb: {wartung.BetriebName}" : "");
        await AenderungspostenAsync(inventarId, inv.InventarNr, inv.Bezeichnung,
            "WartungDurchgefuehrt", null, null, neuerWert, ausfuehrendUser);
    }

    // ── Komponenten speichern ─────────────────────────────────────────────────
    public async Task KomponentenSpeichernAsync(int inventarId, List<InventarKomponente> komponenten, string ausfuehrendUser)
    {
        var inv = await _db.Inventar.FindAsync(inventarId)
            ?? throw new InvalidOperationException("Inventar nicht gefunden.");

        // Replace-Strategie
        var alteKomponenten = await _db.InventarKomponenten
            .Where(k => k.InventarId == inventarId)
            .ToListAsync();
        _db.InventarKomponenten.RemoveRange(alteKomponenten);

        foreach (var k in komponenten)
        {
            k.InventarId = inventarId;
            k.CreatedAt  = DateTime.UtcNow;
            _db.InventarKomponenten.Add(k);
        }
        await _db.SaveChangesAsync();

        await AenderungspostenAsync(inventarId, inv.InventarNr, inv.Bezeichnung,
            "KomponenteGeaendert", null, null, $"{komponenten.Count} Komponente(n)", ausfuehrendUser);
    }

    // ── NoSerie ───────────────────────────────────────────────────────────────
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
