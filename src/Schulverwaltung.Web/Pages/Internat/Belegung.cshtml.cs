using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Pages.Internat;

public class BelegungModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    private readonly InternatService         _internat;
    public BelegungModel(SchulverwaltungDbContext db, InternatService internat)
    { _db = db; _internat = internat; }

    [BindProperty(SupportsGet = true)] public int     BelegungId { get; set; }
    [BindProperty(SupportsGet = true)] public int     VorZimmerId { get; set; }
    [BindProperty(SupportsGet = true)] public string? VorVon     { get; set; }

    [BindProperty] public BelegungFormular Form { get; set; } = new();

    public List<ZimmerAuswahl>   VerfuegbareZimmer { get; set; } = [];
    public List<PersonAuswahl2>  PersonenListe     { get; set; } = [];
    public List<LehrgangAuswahl> LehrgangsListe    { get; set; } = [];
    public bool                  IstNeu            => BelegungId == 0;
    public string?               Fehler            { get; set; }
    public bool?                 ZimmerVerfuegbar  { get; set; }

    // ── GET ───────────────────────────────────────────────────────────────────

    public async Task OnGetAsync()
    {
        await LadeAuswahllisten();

        if (BelegungId != 0)
        {
            var b = await _db.InternatBelegungen.FindAsync(BelegungId);
            if (b != null)
            {
                Form = new BelegungFormular
                {
                    BelegungId   = b.BelegungId,
                    ZimmerId     = b.RaumId ?? 0,
                    PersonId     = b.PersonId,
                    LehrgangId   = b.LehrgangId,
                    BelegungsTyp = b.BelegungsTyp,
                    Von          = b.Von.ToString("yyyy-MM-dd"),
                    Bis          = b.Bis.ToString("yyyy-MM-dd"),
                    KostenArt    = b.KostenArt,
                    Kosten       = b.Kosten,
                    Notiz        = b.Notiz
                };
            }
        }
        else
        {
            // Vorausfüllen aus URL-Params
            Form.ZimmerId = VorZimmerId;
            if (!string.IsNullOrEmpty(VorVon)) Form.Von = VorVon;
        }
    }

    // ── POST ──────────────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAsync()
    {
        await LadeAuswahllisten();

        if (!DateOnly.TryParse(Form.Von, out var von) ||
            !DateOnly.TryParse(Form.Bis, out var bis))
        {
            Fehler = "Von- und Bis-Datum sind Pflichtfelder.";
            return Page();
        }

        if (von > bis)
        {
            Fehler = "Das Startdatum muss vor oder gleich dem Enddatum liegen.";
            return Page();
        }

        if (Form.PersonId == 0)
        {
            Fehler = "Bitte eine Person auswählen.";
            return Page();
        }

        if (Form.ZimmerId == 0)
        {
            Fehler = "Bitte ein Zimmer auswählen.";
            return Page();
        }

        var user = User.Identity?.Name ?? "System";

        try
        {
            if (Form.BelegungId == 0)
            {
                // Neue Belegung
                var belegung = new InternatBelegung
                {
                    RaumId       = Form.ZimmerId == 0 ? null : Form.ZimmerId,
                    PersonId     = Form.PersonId,
                    LehrgangId   = Form.LehrgangId == 0 ? null : Form.LehrgangId,
                    BelegungsTyp = Form.BelegungsTyp,
                    Von          = von,
                    Bis          = bis,
                    KostenArt    = Form.KostenArt,
                    Kosten       = Form.BelegungsTyp == 2 ? 0m : Form.Kosten,
                    Notiz        = Form.Notiz
                };
                await _internat.BelegungErstellenAsync(belegung, user);
            }
            else
            {
                // Bestehende aktualisieren
                await _internat.BelegungAktualisierenAsync(
                    Form.BelegungId, von, bis,
                    Form.BelegungsTyp, Form.KostenArt,
                    Form.BelegungsTyp == 2 ? 0m : Form.Kosten,
                    Form.Notiz, user);
            }
        }
        catch (InvalidOperationException ex)
        {
            Fehler = ex.Message;
            return Page();
        }

        return RedirectToPage("Index");
    }

    // ── Auswahllisten ─────────────────────────────────────────────────────────

    private async Task LadeAuswahllisten()
    {
        var alleRaeume = await _db.Raeume
            .Include(r => r.RaumTyp)
            .Where(r => r.RaumTyp.IstInternat)
            .OrderBy(r => r.RaumNr)
            .ToListAsync();
        VerfuegbareZimmer = alleRaeume.Select(r => {
            var anzeige = r.Bezeichnung != r.RaumNr ? $"{r.RaumNr} · {r.Bezeichnung}" : r.RaumNr;
            return new ZimmerAuswahl(
                r.RaumId,
                $"{anzeige} ({r.Kapazitaet ?? 1} Betten)",
                r.Gesperrt);
        }).ToList();

        PersonenListe = await _db.Personen
            .OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .Select(p => new PersonAuswahl2(p.PersonId, p.PersonNr,
                p.Nachname + ", " + p.Vorname))
            .ToListAsync();

        LehrgangsListe = await _db.Lehrgaenge
            .Where(l => (int)l.Status < 4) // nicht storniert
            .OrderByDescending(l => l.StartDatum)
            .Select(l => new LehrgangAuswahl(l.LehrgangId, l.LehrgangNr, l.Bezeichnung,
                l.Gebuehren))
            .ToListAsync();
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class BelegungFormular
{
    public int      BelegungId   { get; set; }
    public int      ZimmerId     { get; set; }
    public int      PersonId     { get; set; }
    public int?     LehrgangId   { get; set; }
    public byte     BelegungsTyp { get; set; } = 0;
    public string   Von          { get; set; } = "";
    public string   Bis          { get; set; } = "";
    public byte     KostenArt    { get; set; } = 1;
    public decimal? Kosten       { get; set; }
    public string?  Notiz        { get; set; }
}

public record ZimmerAuswahl(int ZimmerId, string Anzeige, bool Gesperrt);
public record PersonAuswahl2(int PersonId, string PersonNr, string Name);
public record LehrgangAuswahl(int LehrgangId, string LehrgangNr, string Bezeichnung, decimal? Gebuehren);
