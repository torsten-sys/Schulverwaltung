using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Infrastructure.Services;

public class AppBenutzerService
{
    private readonly SchulverwaltungDbContext _db;

    public AppBenutzerService(SchulverwaltungDbContext db) => _db = db;

    // ── Rollen-Text ───────────────────────────────────────────────────────────

    public static string RolleText(byte rolle) => rolle switch {
        0 => "Gast",
        1 => "Sachbearbeiter",
        2 => "Dozent",
        3 => "Administrator",
        _ => "Unbekannt"
    };

    // ── Beim Login ────────────────────────────────────────────────────────────

    /// <summary>
    /// Registriert einen Windows-Benutzer beim ersten Anmelden,
    /// oder aktualisiert LetzterLogin bei bekanntem Benutzer.
    /// Wirft Exception wenn Benutzer gesperrt oder ohne Rolle.
    /// </summary>
    public async Task<AppBenutzer> BenutzerAnmeldenAsync(
        string adBenutzername, string displayName, string? email)
    {
        var benutzer = await _db.AppBenutzer
            .FirstOrDefaultAsync(b => b.AdBenutzername == adBenutzername);

        if (benutzer == null)
        {
            // Neuer Benutzer – anlegen mit AppRolle=0
            var belegNr = await NextNoAsync("AENDERUNG");
            benutzer = new AppBenutzer {
                AdBenutzername = adBenutzername,
                DisplayName    = displayName,
                Email          = email,
                AppRolle       = 0,
                ErsterLogin    = DateTime.UtcNow,
                LetzterLogin   = DateTime.UtcNow
            };
            _db.AppBenutzer.Add(benutzer);
            await _db.SaveChangesAsync();

            _db.AppBenutzerAenderungsposten.Add(new AppBenutzerAenderungsposten {
                BelegNr        = belegNr,
                BenutzerId     = benutzer.BenutzerId,
                AdBenutzername = adBenutzername,
                DisplayName    = displayName,
                Ereignis       = "ErsterLogin",
                AusfuehrendUser = adBenutzername
            });
            await _db.SaveChangesAsync();
        }
        else
        {
            benutzer.LetzterLogin = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(displayName) && benutzer.DisplayName != displayName)
                benutzer.DisplayName = displayName;
            if (!string.IsNullOrEmpty(email) && benutzer.Email != email)
                benutzer.Email = email;
            await _db.SaveChangesAsync();

            if (benutzer.Gesperrt)
                throw new UnauthorizedAccessException("GESPERRT");
        }

        return benutzer;
    }

    // ── Rolle setzen ──────────────────────────────────────────────────────────

    public async Task RolleSetzenAsync(int benutzerId, byte neueRolle, string adminUser)
    {
        var benutzer = await _db.AppBenutzer.FindAsync(benutzerId)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");

        var alteRolle = benutzer.AppRolle;
        if (alteRolle == neueRolle) return;

        var belegNr = await NextNoAsync("AENDERUNG");
        benutzer.AppRolle = neueRolle;

        _db.AppBenutzerAenderungsposten.Add(new AppBenutzerAenderungsposten {
            BelegNr         = belegNr,
            BenutzerId      = benutzerId,
            AdBenutzername  = benutzer.AdBenutzername,
            DisplayName     = benutzer.DisplayName,
            Ereignis        = "RolleGeaendert",
            Tabelle         = "AppBenutzer",
            Feld            = "AppRolle",
            AlterWert       = RolleText(alteRolle),
            NeuerWert       = RolleText(neueRolle),
            AusfuehrendUser = adminUser
        });

        await _db.SaveChangesAsync();
    }

    // ── Person verknüpfen ─────────────────────────────────────────────────────

    public async Task PersonVerknuepfenAsync(int benutzerId, int? personId, string adminUser)
    {
        var benutzer = await _db.AppBenutzer.FindAsync(benutzerId)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");

        var belegNr  = await NextNoAsync("AENDERUNG");
        var ereignis = personId.HasValue ? "PersonVerknuepft" : "PersonGeloest";
        benutzer.PersonId = personId;

        _db.AppBenutzerAenderungsposten.Add(new AppBenutzerAenderungsposten {
            BelegNr         = belegNr,
            BenutzerId      = benutzerId,
            AdBenutzername  = benutzer.AdBenutzername,
            DisplayName     = benutzer.DisplayName,
            Ereignis        = ereignis,
            Tabelle         = "AppBenutzer",
            Feld            = "PersonId",
            NeuerWert       = personId?.ToString(),
            AusfuehrendUser = adminUser
        });

        await _db.SaveChangesAsync();
    }

    // ── Sperren / Entsperren ──────────────────────────────────────────────────

    public async Task SperrenAsync(int benutzerId, string sperrGrund, string adminUser)
    {
        var benutzer = await _db.AppBenutzer.FindAsync(benutzerId)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");

        var belegNr = await NextNoAsync("AENDERUNG");
        benutzer.Gesperrt  = true;
        benutzer.SperrGrund = sperrGrund;

        _db.AppBenutzerAenderungsposten.Add(new AppBenutzerAenderungsposten {
            BelegNr         = belegNr,
            BenutzerId      = benutzerId,
            AdBenutzername  = benutzer.AdBenutzername,
            DisplayName     = benutzer.DisplayName,
            Ereignis        = "Gesperrt",
            NeuerWert       = sperrGrund,
            AusfuehrendUser = adminUser
        });

        await _db.SaveChangesAsync();
    }

    public async Task EntsperrenAsync(int benutzerId, string adminUser)
    {
        var benutzer = await _db.AppBenutzer.FindAsync(benutzerId)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");

        var belegNr = await NextNoAsync("AENDERUNG");
        benutzer.Gesperrt   = false;
        benutzer.SperrGrund = null;

        _db.AppBenutzerAenderungsposten.Add(new AppBenutzerAenderungsposten {
            BelegNr         = belegNr,
            BenutzerId      = benutzerId,
            AdBenutzername  = benutzer.AdBenutzername,
            DisplayName     = benutzer.DisplayName,
            Ereignis        = "Entsperrt",
            AusfuehrendUser = adminUser
        });

        await _db.SaveChangesAsync();
    }

    // ── DarfInventarVerwalten ─────────────────────────────────────────────────

    public async Task DarfInventarVerwaltenSetzenAsync(int benutzerId, bool wert, string adminUser)
    {
        var benutzer = await _db.AppBenutzer.FindAsync(benutzerId)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");

        if (benutzer.DarfInventarVerwalten == wert) return;

        var belegNr = await NextNoAsync("AENDERUNG");
        benutzer.DarfInventarVerwalten = wert;

        _db.AppBenutzerAenderungsposten.Add(new AppBenutzerAenderungsposten {
            BelegNr         = belegNr,
            BenutzerId      = benutzerId,
            AdBenutzername  = benutzer.AdBenutzername,
            DisplayName     = benutzer.DisplayName,
            Ereignis        = "InventarZugriffGeaendert",
            Tabelle         = "AppBenutzer",
            Feld            = "DarfInventarVerwalten",
            NeuerWert       = wert ? "Ja" : "Nein",
            AusfuehrendUser = adminUser
        });

        await _db.SaveChangesAsync();
    }

    // ── Notiz speichern ───────────────────────────────────────────────────────

    public async Task NotizSpeichernAsync(int benutzerId, string? notiz)
    {
        var benutzer = await _db.AppBenutzer.FindAsync(benutzerId)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");
        benutzer.Notiz = string.IsNullOrWhiteSpace(notiz) ? null : notiz.Trim();
        await _db.SaveChangesAsync();
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
