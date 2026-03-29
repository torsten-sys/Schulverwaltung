using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Interfaces;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Application.Services;

/// <summary>
/// PersonRolle-Service: Zuweisung und Entfernung von Rollen mit Änderungsprotokoll.
/// BelegNr wird aus der NoSerie 'AENDERUNG' gezogen.
/// </summary>
public class PersonRolleService : IPersonRolleService
{
    private readonly SchulverwaltungDbContext _db;

    public PersonRolleService(SchulverwaltungDbContext db) => _db = db;

    public async Task RolleZuweisenAsync(
        int personId, PersonRolleTyp rolle, int? betriebId, string user,
        CancellationToken ct = default)
    {
        var person = await _db.Personen.FindAsync(new object[] { personId }, ct)
            ?? throw new KeyNotFoundException($"Person {personId} nicht gefunden.");

        var vorhandene = await _db.PersonRollen
            .FirstOrDefaultAsync(r => r.PersonId == personId && r.RolleTyp == rolle && r.Status == 0, ct);

        if (vorhandene != null)
            throw new InvalidOperationException($"Person hat bereits eine aktive Rolle '{rolle}'.");

        _db.PersonRollen.Add(new PersonRolle
        {
            PersonId  = personId,
            RolleTyp  = rolle,
            Status    = 0,
            GueltigAb = DateOnly.FromDateTime(DateTime.Today),
            BetriebId = betriebId
        });

        var belegNr = await NextBelegNrAsync(ct);

        _db.PersonAenderungsposten.Add(new PersonAenderungsposten
        {
            BelegNr         = belegNr,
            PersonId        = personId,
            PersonNr        = person.PersonNr,
            PersonName      = person.AnzeigeName,
            Ereignis        = "RolleZugewiesen",
            Tabelle         = "PersonRolle",
            NeuerWert       = rolle.ToString(),
            RolleTyp        = rolle,
            Zeitstempel     = DateTime.UtcNow,
            AusfuehrendUser = user
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task RolleEntfernenAsync(
        int personId, PersonRolleTyp rolle, string user,
        CancellationToken ct = default)
    {
        var person = await _db.Personen.FindAsync(new object[] { personId }, ct)
            ?? throw new KeyNotFoundException($"Person {personId} nicht gefunden.");

        var vorhandene = await _db.PersonRollen
            .FirstOrDefaultAsync(r => r.PersonId == personId && r.RolleTyp == rolle && r.Status == 0, ct)
            ?? throw new InvalidOperationException($"Keine aktive Rolle '{rolle}' für Person {personId}.");

        vorhandene.Status     = 1;
        vorhandene.GueltigBis = DateOnly.FromDateTime(DateTime.Today);

        var belegNr = await NextBelegNrAsync(ct);

        _db.PersonAenderungsposten.Add(new PersonAenderungsposten
        {
            BelegNr         = belegNr,
            PersonId        = personId,
            PersonNr        = person.PersonNr,
            PersonName      = person.AnzeigeName,
            Ereignis        = "RolleEntzogen",
            Tabelle         = "PersonRolle",
            AlterWert       = rolle.ToString(),
            RolleTyp        = rolle,
            Zeitstempel     = DateTime.UtcNow,
            AusfuehrendUser = user
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> HatAktiveRolleAsync(
        int personId, PersonRolleTyp rolle,
        CancellationToken ct = default)
        => await _db.PersonRollen
            .AnyAsync(r => r.PersonId == personId && r.RolleTyp == rolle && r.Status == 0, ct);

    // ── Hilfsmethoden ────────────────────────────────────────────────────────

    private async Task<string> NextBelegNrAsync(CancellationToken ct)
    {
        var zeile = await _db.NoSerieZeilen
            .Where(z => z.NoSerieCode == "AENDERUNG" && z.Offen)
            .FirstOrDefaultAsync(ct);

        if (zeile == null)
            return Guid.NewGuid().ToString("N")[..20]; // Fallback wenn NoSerie nicht eingerichtet

        long naechste = zeile.LastNoUsed == null
            ? long.Parse(zeile.StartingNo[(zeile.Prefix?.Length ?? 0)..])
            : long.Parse(zeile.LastNoUsed[(zeile.Prefix?.Length ?? 0)..]) + zeile.IncrementBy;

        var nr = (zeile.Prefix ?? "") + naechste.ToString().PadLeft(zeile.NummerLaenge, '0');
        zeile.LastNoUsed   = nr;
        zeile.LastDateUsed = DateOnly.FromDateTime(DateTime.Today);
        return nr;
    }
}
