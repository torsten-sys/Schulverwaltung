using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class MeisterFunktionenModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public MeisterFunktionenModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public int    LehrgangId { get; set; }
    public string LehrgangNr { get; set; } = "";
    public string Fehler     { get; set; } = "";

    public List<FunktionZeile>      Funktionen { get; set; } = [];
    public List<VerlaufZeile>       Verlauf    { get; set; } = [];
    public List<TeilnehmerAuswahl>  Teilnehmer { get; set; } = [];

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

    public async Task<IActionResult> OnPostFunktionAendernAsync(
        int lehrgangId, byte funktion, int personId)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 1)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var user  = User.Identity?.Name ?? "System";
        var heute = DateOnly.FromDateTime(DateTime.Today);

        var alt = await _db.MeisterFunktionen
            .Where(f => f.LehrgangId == lehrgangId && f.Funktion == funktion && f.GueltigBis == null)
            .FirstOrDefaultAsync();
        if (alt != null) alt.GueltigBis = heute;

        if (personId > 0)
        {
            var person = await _db.Personen.FindAsync(personId);
            if (person != null)
            {
                _db.MeisterFunktionen.Add(new MeisterFunktion
                {
                    LehrgangId = lehrgangId,
                    PersonId   = personId,
                    PersonNr   = person.PersonNr,
                    PersonName = person.Nachname + ", " + person.Vorname,
                    Funktion   = funktion,
                    GueltigAb  = heute,
                    CreatedBy  = user,
                    CreatedAt  = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = lehrgangId });
    }

    private async Task LadeDaten()
    {
        var alleFunktionen = await _db.MeisterFunktionen
            .Where(f => f.LehrgangId == LehrgangId)
            .OrderByDescending(f => f.GueltigAb)
            .ToListAsync();

        var aktiv = alleFunktionen.Where(f => f.GueltigBis == null).ToList();

        Funktionen = Enumerable.Range(0, 10).Select(i =>
        {
            var a = aktiv.FirstOrDefault(f => f.Funktion == i);
            return new FunktionZeile(
                (byte)i,
                MeisterkursService.FunktionName((byte)i),
                a?.FunktionId,
                a?.PersonId,
                a?.PersonName,
                a?.GueltigAb);
        }).ToList();

        Verlauf = alleFunktionen.Select(f => new VerlaufZeile(
            MeisterkursService.FunktionName(f.Funktion),
            f.PersonName, f.GueltigAb, f.GueltigBis)).ToList();

        var teilnehmerPersonen = await _db.LehrgangPersonen
            .Where(p => p.LehrgangId == LehrgangId && p.Rolle == LehrgangPersonRolle.Teilnehmer)
            .Include(p => p.Person)
            .OrderBy(p => p.Person.Nachname)
            .ToListAsync();

        Teilnehmer = teilnehmerPersonen.Select(p => new TeilnehmerAuswahl(
            p.PersonId,
            p.Person.Nachname + ", " + p.Person.Vorname)).ToList();
    }

    public record FunktionZeile(
        byte Funktion, string FunktionName,
        int? FunktionId, int? PersonId, string? PersonName, DateOnly? GueltigAb);

    public record VerlaufZeile(
        string FunktionName, string PersonName, DateOnly GueltigAb, DateOnly? GueltigBis);

    public record TeilnehmerAuswahl(int PersonId, string Name);
}
