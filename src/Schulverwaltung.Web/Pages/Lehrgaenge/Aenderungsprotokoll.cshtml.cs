using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class AenderungsprotokollModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public AenderungsprotokollModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public int    LehrgangId { get; set; }
    public string LehrgangNr { get; set; } = "";

    public List<PostenZeile> Posten { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle == 0)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var lehrgang = await _db.Lehrgaenge.FindAsync(Id);
        if (lehrgang == null) return NotFound();

        LehrgangId = lehrgang.LehrgangId;
        LehrgangNr = lehrgang.LehrgangNr;

        var posten = await _db.LehrgangAenderungsposten
            .Where(p => p.LehrgangId == Id)
            .OrderByDescending(p => p.Zeitstempel)
            .ToListAsync();

        Posten = posten.Select(p => new PostenZeile(
            p.PostenId, p.BelegNr, p.Ereignis, p.Tabelle,
            p.Feld, p.AlterWert, p.NeuerWert,
            p.Zeitstempel, p.AusfuehrendUser
        )).ToList();

        return Page();
    }

    public record PostenZeile(
        int PostenId, string BelegNr, string Ereignis, string? Tabelle,
        string? Feld, string? AlterWert, string? NeuerWert,
        DateTime Zeitstempel, string User);
}
