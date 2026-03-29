using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class MeisterEinladungDruckModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public MeisterEinladungDruckModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public string LehrgangNr { get; set; } = "";
    public List<EinladungDoc> Dokumente { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle == 0)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var lehrgang = await _db.Lehrgaenge.FindAsync(Id);
        if (lehrgang == null) return NotFound();
        LehrgangNr = lehrgang.LehrgangNr;

        var vorlage = await _db.EinladungsVorlagen.FindAsync(1) ?? new EinladungsVorlage();

        var personen = await _db.LehrgangPersonen
            .Where(lp => lp.LehrgangId == Id && lp.Rolle == LehrgangPersonRolle.Teilnehmer)
            .Include(lp => lp.Person)
            .OrderBy(lp => lp.Person.Nachname).ThenBy(lp => lp.Person.Vorname)
            .ToListAsync();

        var personIds  = personen.Select(p => p.PersonId).ToList();
        var belegungen = await _db.InternatBelegungen
            .Where(b => b.LehrgangId == Id && personIds.Contains(b.PersonId))
            .Include(b => b.Raum).ThenInclude(r => r!.RaumTyp)
            .ToListAsync();

        foreach (var lp in personen)
        {
            var belegung = belegungen.FirstOrDefault(b => b.PersonId == lp.PersonId);
            var vars     = EinladungsVariablenResolver.Erstelle(lehrgang, lp.Person, belegung);

            Dokumente.Add(new EinladungDoc(
                lp.Person.Nachname + ", " + lp.Person.Vorname,
                EinladungsVariablenResolver.Ersetze(vorlage.Anschreiben, vars),
                EinladungsVariablenResolver.Ersetze(vorlage.ZahlungsplanText, vars),
                belegung != null ? EinladungsVariablenResolver.Ersetze(vorlage.InternatAbschnitt, vars) : null,
                EinladungsVariablenResolver.Ersetze(vorlage.RatenplanText, vars),
                EinladungsVariablenResolver.Ersetze(vorlage.Schlusstext, vars)
            ));
        }

        return Page();
    }

    public record EinladungDoc(
        string Name,
        string Anschreiben, string Zahlungsplan,
        string? Internat,
        string Ratenplan, string Schlusstext);
}
