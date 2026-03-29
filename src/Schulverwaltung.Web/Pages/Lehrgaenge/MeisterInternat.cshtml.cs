using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class MeisterInternatModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public MeisterInternatModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public int    LehrgangId { get; set; }
    public string LehrgangNr { get; set; } = "";

    public List<BelegungZeile> Belegungen { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle == 0)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var lehrgang = await _db.Lehrgaenge.FindAsync(Id);
        if (lehrgang == null) return NotFound();

        LehrgangId = lehrgang.LehrgangId;
        LehrgangNr = lehrgang.LehrgangNr;
        await LadeDaten();
        return Page();
    }

    private async Task LadeDaten()
    {
        var belegungen = await _db.InternatBelegungen
            .Where(b => b.LehrgangId == LehrgangId)
            .Include(b => b.Raum)
            .ThenInclude(r => r!.RaumTyp)
            .OrderBy(b => b.Von)
            .ThenBy(b => b.PersonName)
            .ToListAsync();

        Belegungen = belegungen.Select(b =>
        {
            var bezahltStatus  = b.Bezahlt ? "Bezahlt" : "Offen";
            var bezahltStyle   = b.Bezahlt
                ? "background:#dff6dd;color:#107C10"
                : "background:#fff4ce;color:#8a6914";
            var zimmerBez = b.Raum != null
                ? $"{b.Raum.RaumNr} – {b.Raum.Bezeichnung}"
                : "– kein Zimmer –";
            var zimmerTyp = b.Raum?.RaumTyp?.Bezeichnung ?? "–";
            return new BelegungZeile(
                b.BelegungId, b.PersonName,
                zimmerBez, zimmerTyp,
                b.Von, b.Bis,
                bezahltStatus, bezahltStyle);
        }).ToList();
    }

    public record BelegungZeile(
        int BelegungId, string PersonName,
        string ZimmerBezeichnung, string ZimmerTyp,
        DateOnly EinzugDatum, DateOnly? AuszugDatum,
        string StatusText, string StatusStyle);
}
