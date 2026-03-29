using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class KursmoduleModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public KursmoduleModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public int    LehrgangId  { get; set; }
    public string LehrgangNr  { get; set; } = "";
    public string Fehler      { get; set; } = "";

    public List<AbschnittZeile>      Abschnitte         { get; set; } = [];
    public List<PvZuordnungZeile>    Patientenzuordnungen { get; set; } = [];

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

    public async Task<IActionResult> OnPostSpeichernAsync(
        int lehrgangId,
        Dictionary<int, string> Bezeichnungen,
        Dictionary<int, byte> Status)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 1)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var abschnitte = await _db.MeisterAbschnitte
            .Where(a => a.LehrgangId == lehrgangId)
            .ToListAsync();

        foreach (var a in abschnitte)
        {
            if (Bezeichnungen.TryGetValue(a.AbschnittId, out var bez) && !string.IsNullOrWhiteSpace(bez))
                a.Bezeichnung = bez.Trim();
            if (Status.TryGetValue(a.AbschnittId, out var stat))
                a.Status = stat;
        }

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = lehrgangId });
    }

    private async Task LadeDaten()
    {
        var abschnitte = await _db.MeisterAbschnitte
            .Where(a => a.LehrgangId == LehrgangId)
            .OrderBy(a => a.Reihenfolge)
            .ToListAsync();

        Abschnitte = abschnitte.Select(a => new AbschnittZeile(
            a.AbschnittId, a.Nummer, a.Bezeichnung, a.AbschnittTyp, a.Status
        )).ToList();

        var pvIds = abschnitte
            .Where(a => a.AbschnittTyp >= 1)
            .Select(a => a.AbschnittId)
            .ToList();

        if (pvIds.Any())
        {
            var zuordnungen = await _db.MeisterPatientenZuordnungen
                .Where(z => pvIds.Contains(z.AbschnittId))
                .Include(z => z.Termine)
                .OrderBy(z => z.PatientName)
                .ToListAsync();

            Patientenzuordnungen = zuordnungen.Select(z =>
            {
                var ersterTermin = z.Termine.OrderBy(t => t.Datum).FirstOrDefault();
                return new PvZuordnungZeile(
                    z.ZuordnungId, z.AbschnittId,
                    z.PatientName,
                    z.Meisterschueler1Name,
                    z.BuchungsStatus,
                    ersterTermin?.Datum);
            }).ToList();
        }
    }

    public record AbschnittZeile(int AbschnittId, int Nummer, string Bezeichnung, byte AbschnittTyp, byte Status);

    public record PvZuordnungZeile(
        int ZuordnungId, int AbschnittId,
        string PatientName, string MeisterschuelerName,
        byte BuchungsStatus, DateOnly? TerminDatum);
}
