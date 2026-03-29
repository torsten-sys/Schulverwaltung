using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Pages.Internat;

public class ZimmerModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    private readonly InternatService         _internat;
    public ZimmerModel(SchulverwaltungDbContext db, InternatService internat)
    { _db = db; _internat = internat; }

    public List<ZimmerZeile>          ZimmerListe  { get; set; } = [];
    public InternatStats              Stats        { get; set; } = new();
    public Dictionary<int, List<InternatAenderungsposten>> Protokoll { get; set; } = [];
    public string? Fehler { get; set; }

    // ── GET ───────────────────────────────────────────────────────────────────

    public async Task OnGetAsync()
    {
        await LadeDaten();
    }

    // ── POST: Zimmer speichern (neu/bearbeiten) ───────────────────────────────

    public async Task<IActionResult> OnPostSpeichernAsync(
        int raumId, string zimmerNr, string? bezeichnung, int kapazitaet, string? notiz)
    {
        if (string.IsNullOrWhiteSpace(zimmerNr))
        {
            Fehler = "Zimmernummer ist Pflichtfeld.";
            await LadeDaten();
            return Page();
        }

        if (raumId == 0)
        {
            var raumTyp = await _db.RaumTypen.FirstOrDefaultAsync(t => t.Code == "INTERNAT");
            if (raumTyp == null)
            {
                Fehler = "Raumtyp 'INTERNAT' nicht gefunden. Bitte zuerst SQL-Migration ausführen.";
                await LadeDaten();
                return Page();
            }

            _db.Raeume.Add(new Raum
            {
                RaumNr      = zimmerNr.Trim(),
                Bezeichnung = string.IsNullOrWhiteSpace(bezeichnung) ? zimmerNr.Trim() : bezeichnung.Trim(),
                RaumTypId   = raumTyp.RaumTypId,
                Kapazitaet  = Math.Max(1, kapazitaet),
                Notiz       = notiz
            });
        }
        else
        {
            var r = await _db.Raeume.FindAsync(raumId);
            if (r != null)
            {
                r.RaumNr      = zimmerNr.Trim();
                r.Bezeichnung = string.IsNullOrWhiteSpace(bezeichnung) ? zimmerNr.Trim() : bezeichnung.Trim();
                r.Kapazitaet  = Math.Max(1, kapazitaet);
                r.Notiz       = notiz;
            }
        }

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            Fehler = "Zimmernummer ist bereits vergeben.";
            await LadeDaten();
            return Page();
        }

        return RedirectToPage();
    }

    // ── POST: Sperren ─────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostSperrenAsync(int zimmerId, string sperrGrund)
    {
        var user = User.Identity?.Name ?? "System";
        try { await _internat.ZimmerSperrenAsync(zimmerId, sperrGrund?.Trim() ?? "", user); }
        catch (InvalidOperationException ex)
        {
            Fehler = ex.Message;
            await LadeDaten();
            return Page();
        }
        return RedirectToPage();
    }

    // ── POST: Entsperren ──────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostEntsperrenAsync(int zimmerId)
    {
        var user = User.Identity?.Name ?? "System";
        try { await _internat.ZimmerEntsperrenAsync(zimmerId, user); }
        catch (InvalidOperationException ex)
        {
            Fehler = ex.Message;
            await LadeDaten();
            return Page();
        }
        return RedirectToPage();
    }

    // ── POST: Löschen ─────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostLoeschenAsync(int zimmerId)
    {
        var hatBelegungen = await _db.InternatBelegungen.AnyAsync(b => b.RaumId == zimmerId);
        if (hatBelegungen)
        {
            Fehler = "Zimmer kann nicht gelöscht werden, da noch Belegungen vorhanden sind.";
            await LadeDaten();
            return Page();
        }

        var r = await _db.Raeume.FindAsync(zimmerId);
        if (r != null) { _db.Raeume.Remove(r); await _db.SaveChangesAsync(); }
        return RedirectToPage();
    }

    // ── Daten laden ───────────────────────────────────────────────────────────

    private async Task LadeDaten()
    {
        var heute = DateOnly.FromDateTime(DateTime.Today);
        var raeume = await _db.Raeume
            .Include(r => r.RaumTyp)
            .Where(r => r.RaumTyp.IstInternat)
            .OrderBy(r => r.RaumNr)
            .ToListAsync();

        // Aktuelle Belegungen pro Raum
        var aktuell = await _db.InternatBelegungen
            .Where(b => b.Von <= heute && b.Bis >= heute)
            .GroupBy(b => b.RaumId)
            .Select(g => new { RaumId = g.Key, Anzahl = g.Count() })
            .ToListAsync();
        var aktuellDict = aktuell
            .Where(x => x.RaumId.HasValue)
            .ToDictionary(x => x.RaumId!.Value, x => x.Anzahl);

        ZimmerListe = raeume.Select(r => new ZimmerZeile(
            r.RaumId, r.RaumNr, r.Bezeichnung, r.Kapazitaet ?? 1,
            r.Gesperrt, r.SperrGrund, r.Notiz,
            aktuellDict.TryGetValue(r.RaumId, out var anz) ? anz : 0
        )).ToList();

        Stats = new InternatStats
        {
            ZimmerGesamt = raeume.Count,
            BettenGesamt = raeume.Sum(r => r.Kapazitaet ?? 1),
            HeuteBelegt  = aktuell.Sum(x => x.Anzahl),
            HeuteFrei    = Math.Max(0, raeume.Sum(r => r.Kapazitaet ?? 1) - aktuell.Sum(x => x.Anzahl))
        };

        // Protokoll per Raum (ZimmerId im Snapshot = RaumId)
        var allePosten = await _db.InternatAenderungsposten
            .OrderByDescending(p => p.Zeitstempel)
            .ToListAsync();
        Protokoll = allePosten
            .GroupBy(p => p.ZimmerId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ZimmerZeile(
    int RaumId, string ZimmerNr, string Bezeichnung, int Kapazitaet,
    bool Gesperrt, string? SperrGrund, string? Notiz,
    int AktuellBelegt)
{
    public string Anzeige => Bezeichnung != ZimmerNr ? $"{ZimmerNr} · {Bezeichnung}" : ZimmerNr;
}
