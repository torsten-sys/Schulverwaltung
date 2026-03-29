using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Berichte;

public class VorschauModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public VorschauModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string Key        { get; set; } = "";
    [BindProperty(SupportsGet = true)] public int    Id         { get; set; }
    [BindProperty(SupportsGet = true)] public int?   VorlageId  { get; set; }

    public string  Titel   { get; set; } = "Vorschau";
    public string? KopfHtml { get; set; }
    public string? FussHtml  { get; set; }

    // Daten für lehrgang-teilnehmerliste
    public Lehrgang?              Lehrgang   { get; set; }
    public List<LehrgangPerson>   Teilnehmer { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        // Mindestens Gast-Zugriff
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null)
            return RedirectToPage("/Zugriff/KeinZugriff");

        // Briefvorlage laden
        Briefvorlage? vorlage;
        if (VorlageId.HasValue)
            vorlage = await _db.Briefvorlagen.FindAsync(VorlageId.Value);
        else
            vorlage = await _db.Briefvorlagen
                .Where(v => v.IstStandard && !v.Gesperrt)
                .FirstOrDefaultAsync();

        KopfHtml = vorlage?.KopfHtml;
        FussHtml  = vorlage?.FussHtml;

        // Dokument-spezifische Daten laden
        switch (Key)
        {
            case "lehrgang-teilnehmerliste":
                Lehrgang = await _db.Lehrgaenge
                    .Include(l => l.Art)
                    .FirstOrDefaultAsync(l => l.LehrgangId == Id);
                if (Lehrgang == null) return NotFound();

                Titel = $"Teilnehmerliste – {Lehrgang.Bezeichnung}";

                Teilnehmer = await _db.LehrgangPersonen
                    .Include(lp => lp.Person)
                    .Where(lp => lp.LehrgangId == Id && lp.Rolle == LehrgangPersonRolle.Teilnehmer)
                    .OrderBy(lp => lp.Person.Nachname)
                    .ThenBy(lp => lp.Person.Vorname)
                    .ToListAsync();
                break;

            default:
                return NotFound();
        }

        return Page();
    }
}
