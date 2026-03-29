using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Pages.Internat;

public class IndexModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    private readonly InternatService         _internat;
    public IndexModel(SchulverwaltungDbContext db, InternatService internat)
    { _db = db; _internat = internat; }

    public bool DarfBearbeiten =>
        HttpContext.Items["AppBenutzer"] is AppBenutzer b && b.AppRolle >= 1 && !b.Gesperrt;

    // ── Filter / Navigation ───────────────────────────────────────────────────
    [BindProperty(SupportsGet = true)] public int    Monat     { get; set; }
    [BindProperty(SupportsGet = true)] public int    Jahr      { get; set; }
    [BindProperty(SupportsGet = true)] public string ActiveTab { get; set; } = "plan";
    [BindProperty(SupportsGet = true)] public byte?  FilterTyp { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterBezahlt { get; set; }

    // ── Daten ─────────────────────────────────────────────────────────────────
    public InternatStats          Stats         { get; set; } = new();
    public List<int>              TageImMonat   { get; set; } = [];
    public List<ZimmerPlanZeile>  ZimmerPlan    { get; set; } = [];
    public List<BelegungsListeZeile> Belegungsliste { get; set; } = [];
    public string? Fehler { get; set; }

    // ── GET ───────────────────────────────────────────────────────────────────

    public async Task OnGetAsync()
    {
        if (Monat == 0) Monat = DateTime.Today.Month;
        if (Jahr  == 0) Jahr  = DateTime.Today.Year;

        await LadeDaten();
    }

    // ── POST: Stornieren ──────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostStornierenAsync(int belegungId)
    {
        var user = User.Identity?.Name ?? "System";
        try { await _internat.BelegungStornierenAsync(belegungId, user); }
        catch (InvalidOperationException ex) { Fehler = ex.Message; }

        return RedirectToPage(new { Monat, Jahr, ActiveTab = "liste" });
    }

    // ── POST: Bezahlt markieren ───────────────────────────────────────────────

    public async Task<IActionResult> OnPostBezahltAsync(int belegungId)
    {
        var user = User.Identity?.Name ?? "System";
        try { await _internat.BezahltMarkierenAsync(belegungId, user); }
        catch (InvalidOperationException ex) { Fehler = ex.Message; }

        return RedirectToPage(new { Monat, Jahr, ActiveTab = "liste" });
    }

    // ── Daten laden ───────────────────────────────────────────────────────────

    private async Task LadeDaten()
    {
        var heute     = DateOnly.FromDateTime(DateTime.Today);
        var monatsStart = new DateOnly(Jahr, Monat, 1);
        var monatsEnd   = monatsStart.AddMonths(1).AddDays(-1);
        int tage        = monatsEnd.Day;
        TageImMonat     = Enumerable.Range(1, tage).ToList();

        var alleZimmer = await _db.Raeume
            .Include(r => r.RaumTyp)
            .Where(r => r.RaumTyp.IstInternat)
            .OrderBy(r => r.RaumNr)
            .ToListAsync();

        // Stats
        var belegungenHeute = await _db.InternatBelegungen
            .Where(b => b.Von <= heute && b.Bis >= heute)
            .CountAsync();
        int bettenGesamt = alleZimmer.Sum(z => z.Kapazitaet ?? 1);

        Stats = new InternatStats
        {
            ZimmerGesamt = alleZimmer.Count,
            BettenGesamt = bettenGesamt,
            HeuteBelegt  = belegungenHeute,
            HeuteFrei    = Math.Max(0, bettenGesamt - belegungenHeute)
        };

        // Belegungsplan – alle Belegungen im Monat
        var monatsBeleg = await _db.InternatBelegungen
            .Where(b => b.Von <= monatsEnd && b.Bis >= monatsStart)
            .ToListAsync();

        ZimmerPlan = alleZimmer.Select(z =>
        {
            var belegungenFuerZimmer = monatsBeleg.Where(b => b.RaumId == z.RaumId).ToList();
            var tageDict = new Dictionary<int, List<BelegungPlanInfo>>();
            foreach (var tag in TageImMonat)
            {
                var datum = new DateOnly(Jahr, Monat, tag);
                var bAmTag = belegungenFuerZimmer
                    .Where(b => b.Von <= datum && b.Bis >= datum)
                    .Select(b => new BelegungPlanInfo(b.BelegungId, b.PersonName, b.BelegungsTyp))
                    .ToList();
                if (bAmTag.Any()) tageDict[tag] = bAmTag;
            }
            return new ZimmerPlanZeile(
                z.RaumId, z.RaumNr, z.Bezeichnung, z.Kapazitaet ?? 1,
                z.Gesperrt, z.SperrGrund, tageDict);
        }).ToList();

        // Belegungsliste
        var listeQuery = _db.InternatBelegungen.AsQueryable();

        if (FilterTyp.HasValue)
            listeQuery = listeQuery.Where(b => b.BelegungsTyp == FilterTyp.Value);
        if (FilterBezahlt == "ja")
            listeQuery = listeQuery.Where(b => b.Bezahlt);
        else if (FilterBezahlt == "nein")
            listeQuery = listeQuery.Where(b => !b.Bezahlt);

        var belegRaw = await listeQuery
            .Include(b => b.Raum)
            .OrderByDescending(b => b.Von)
            .ToListAsync();

        Belegungsliste = belegRaw.Select(b => {
            var anzeige = b.Raum != null
                ? b.Raum.Bezeichnung != b.Raum.RaumNr
                    ? $"{b.Raum.RaumNr} · {b.Raum.Bezeichnung}"
                    : b.Raum.RaumNr
                : "";
            return new BelegungsListeZeile(
                b.BelegungId, b.RaumId ?? 0, b.Raum?.RaumNr ?? "", anzeige,
                b.PersonNr, b.PersonName, b.LehrgangNr,
                b.BelegungsTyp, b.Von, b.Bis,
                b.KostenArt, b.Kosten, b.Bezahlt, b.BezahltAm, b.Notiz);
        }).ToList();
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class InternatStats
{
    public int ZimmerGesamt { get; set; }
    public int BettenGesamt { get; set; }
    public int HeuteBelegt  { get; set; }
    public int HeuteFrei    { get; set; }
}

public record BelegungPlanInfo(int BelegungId, string PersonName, byte BelegungsTyp);

public record ZimmerPlanZeile(
    int RaumId, string ZimmerNr, string Bezeichnung, int Kapazitaet,
    bool Gesperrt, string? SperrGrund,
    Dictionary<int, List<BelegungPlanInfo>> Tage)
{
    public string Anzeige => Bezeichnung != ZimmerNr ? $"{ZimmerNr} · {Bezeichnung}" : ZimmerNr;
}

public record BelegungsListeZeile(
    int BelegungId, int ZimmerId, string ZimmerNr, string ZimmerAnzeige,
    string PersonNr, string PersonName, string? LehrgangNr,
    byte BelegungsTyp, DateOnly Von, DateOnly Bis,
    byte KostenArt, decimal? Kosten, bool Bezahlt, DateOnly? BezahltAm, string? Notiz);
