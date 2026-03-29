using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Betriebe;

public class IndexModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public IndexModel(SchulverwaltungDbContext db) => _db = db;

    public bool DarfBearbeiten =>
        HttpContext.Items["AppBenutzer"] is AppBenutzer b && b.AppRolle >= 1 && !b.Gesperrt;

    public IReadOnlyList<BetriebZeile> Betriebe { get; set; } = [];

    public async Task OnGetAsync()
    {
        var liste = await _db.Betriebe
            .OrderBy(b => b.Name)
            .ToListAsync();

        // AnzahlTeilnehmer: Personen mit aktiver Teilnehmer-Rolle für diesen Betrieb
        var betriebIds = liste.Select(b => b.BetriebId).ToList();
        var personCounts = await _db.PersonRollen
            .Where(r => r.BetriebId != null && betriebIds.Contains(r.BetriebId!.Value)
                        && r.RolleTyp == PersonRolleTyp.Teilnehmer && r.Status == 0)
            .GroupBy(r => r.BetriebId!.Value)
            .Select(g => new { BetriebId = g.Key, Anzahl = g.Count() })
            .ToListAsync();

        var countDict = personCounts.ToDictionary(x => x.BetriebId, x => x.Anzahl);

        Betriebe = liste.Select(b => new BetriebZeile(
            b.BetriebId, b.BetriebNr, b.Name,
            b.PLZ, b.Ort, b.Telefon, b.Email,
            countDict.GetValueOrDefault(b.BetriebId, 0), b.Gesperrt
        )).ToList();
    }

    public record BetriebZeile(
        int BetriebId, string BetriebNr, string Name,
        string? PLZ, string? Ort, string? Telefon, string? Email,
        int AnzahlTeilnehmer, bool Gesperrt);
}
