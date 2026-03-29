using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class MeisterEinladungenModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public MeisterEinladungenModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public int    LehrgangId { get; set; }
    public string LehrgangNr { get; set; } = "";

    public List<TeilnehmerZeile> Teilnehmer { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle == 0)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var lehrgang = await _db.Lehrgaenge.FindAsync(Id);
        if (lehrgang == null) return NotFound();

        LehrgangId = lehrgang.LehrgangId;
        LehrgangNr = lehrgang.LehrgangNr;

        var personen = await _db.LehrgangPersonen
            .Where(lp => lp.LehrgangId == Id && lp.Rolle == LehrgangPersonRolle.Teilnehmer)
            .Include(lp => lp.Person)
            .OrderBy(lp => lp.Person.Nachname).ThenBy(lp => lp.Person.Vorname)
            .ToListAsync();

        var personIds  = personen.Select(p => p.PersonId).ToList();
        var einladungen = await _db.LehrgangEinladungen
            .Where(e => e.LehrgangId == Id && personIds.Contains(e.PersonId))
            .ToListAsync();

        Teilnehmer = personen.Select(lp =>
        {
            var ein = einladungen.FirstOrDefault(e => e.PersonId == lp.PersonId);
            return new TeilnehmerZeile(
                lp.PersonId,
                lp.Person.Nachname + ", " + lp.Person.Vorname,
                lp.BetriebName ?? "–",
                ein?.Status ?? 255,   // 255 = noch nicht erstellt
                ein?.ErstelltAm,
                ein?.GesendetAm);
        }).ToList();

        return Page();
    }

    public record TeilnehmerZeile(
        int PersonId, string Name, string Betrieb,
        byte Status, DateTime? ErstelltAm, DateTime? GesendetAm);
}
