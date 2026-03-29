using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class MeisterEinladungModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public MeisterEinladungModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int LehrgangId { get; set; }
    [BindProperty(SupportsGet = true)] public int PersonId   { get; set; }

    public string LehrgangNr   { get; set; } = "";
    public string PersonName   { get; set; } = "";
    public byte   EinladStatus { get; set; } = 0;
    public string? Fehler      { get; set; }

    // Rendered sections
    public string HtmlAnschreiben     { get; set; } = "";
    public string HtmlZahlungsplan    { get; set; } = "";
    public string HtmlInternat        { get; set; } = "";
    public bool   HatInternat         { get; set; }
    public string HtmlRatenplan       { get; set; } = "";
    public string HtmlSchlusstext     { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle == 0)
            return RedirectToPage("/Zugriff/KeinZugriff");

        if (!await LadeDaten()) return NotFound();

        // Ensure LehrgangEinladung record exists
        var ein = await _db.LehrgangEinladungen
            .FirstOrDefaultAsync(e => e.LehrgangId == LehrgangId && e.PersonId == PersonId);
        if (ein == null)
        {
            ein = new LehrgangEinladung { LehrgangId = LehrgangId, PersonId = PersonId };
            _db.LehrgangEinladungen.Add(ein);
            await _db.SaveChangesAsync();
        }
        EinladStatus = ein.Status;
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(byte status)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle == 0)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var ein = await _db.LehrgangEinladungen
            .FirstOrDefaultAsync(e => e.LehrgangId == LehrgangId && e.PersonId == PersonId);
        if (ein == null)
        {
            ein = new LehrgangEinladung { LehrgangId = LehrgangId, PersonId = PersonId };
            _db.LehrgangEinladungen.Add(ein);
        }
        ein.Status = status;
        if (status == 1 && ein.GesendetAm == null) ein.GesendetAm = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToPage(new { LehrgangId, PersonId });
    }

    private async Task<bool> LadeDaten()
    {
        var lehrgang = await _db.Lehrgaenge.FindAsync(LehrgangId);
        if (lehrgang == null) return false;

        var person = await _db.Personen.FindAsync(PersonId);
        if (person == null) return false;

        LehrgangNr  = lehrgang.LehrgangNr;
        PersonName  = person.Nachname + ", " + person.Vorname;

        var vorlage = await _db.EinladungsVorlagen.FindAsync(1)
                      ?? new EinladungsVorlage();

        var belegung = await _db.InternatBelegungen
            .Where(b => b.LehrgangId == LehrgangId && b.PersonId == PersonId)
            .Include(b => b.Raum).ThenInclude(r => r!.RaumTyp)
            .FirstOrDefaultAsync();

        var vars = EinladungsVariablenResolver.Erstelle(lehrgang, person, belegung);

        HtmlAnschreiben  = EinladungsVariablenResolver.Ersetze(vorlage.Anschreiben, vars);
        HtmlZahlungsplan = EinladungsVariablenResolver.Ersetze(vorlage.ZahlungsplanText, vars);
        HatInternat      = belegung != null;
        HtmlInternat     = belegung != null
            ? EinladungsVariablenResolver.Ersetze(vorlage.InternatAbschnitt, vars)
            : "";
        HtmlRatenplan    = EinladungsVariablenResolver.Ersetze(vorlage.RatenplanText, vars);
        HtmlSchlusstext  = EinladungsVariablenResolver.Ersetze(vorlage.Schlusstext, vars);

        return true;
    }
}
