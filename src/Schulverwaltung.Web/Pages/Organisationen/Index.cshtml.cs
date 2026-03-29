using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Organisationen;

public class IndexModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public IndexModel(SchulverwaltungDbContext db) => _db = db;

    public bool DarfBearbeiten =>
        HttpContext.Items["AppBenutzer"] is AppBenutzer b && b.AppRolle >= 1 && !b.Gesperrt;

    [BindProperty(SupportsGet = true)] public string? Suche { get; set; }
    [BindProperty(SupportsGet = true)] public string? Typ   { get; set; }  // "", "0", "1"

    public IReadOnlyList<OrganisationZeile> Organisationen { get; set; } = [];

    public async Task OnGetAsync()
    {
        var query = _db.Organisationen.AsQueryable();

        if (Typ == "0") query = query.Where(o => o.OrganisationsTyp == 0);
        else if (Typ == "1") query = query.Where(o => o.OrganisationsTyp == 1);

        if (!string.IsNullOrWhiteSpace(Suche))
        {
            var s = Suche.Trim().ToLower();
            query = query.Where(o =>
                o.Name.ToLower().Contains(s) ||
                (o.Kurzbezeichnung != null && o.Kurzbezeichnung.ToLower().Contains(s)) ||
                o.OrganisationsNr.ToLower().Contains(s));
        }

        var liste = await query.OrderBy(o => o.Name).ToListAsync();

        var orgIds = liste.Select(o => o.OrganisationId).ToList();

        // Anzahl Betriebe: via InnungsId ODER HandwerkskammerId
        var innungCounts = await _db.Betriebe
            .Where(b => b.InnungsId != null && orgIds.Contains(b.InnungsId!.Value))
            .GroupBy(b => b.InnungsId!.Value)
            .Select(g => new { OrgId = g.Key, Anzahl = g.Count() })
            .ToListAsync();

        var hwkCounts = await _db.Betriebe
            .Where(b => b.HandwerkskammerId != null && orgIds.Contains(b.HandwerkskammerId!.Value))
            .GroupBy(b => b.HandwerkskammerId!.Value)
            .Select(g => new { OrgId = g.Key, Anzahl = g.Count() })
            .ToListAsync();

        var innungDict = innungCounts.ToDictionary(x => x.OrgId, x => x.Anzahl);
        var hwkDict    = hwkCounts.ToDictionary(x => x.OrgId, x => x.Anzahl);

        Organisationen = liste.Select(o => new OrganisationZeile(
            o.OrganisationId, o.OrganisationsNr, o.OrganisationsTyp, o.Name,
            o.Kurzbezeichnung, o.Ort, o.Email, o.Sammelrechnung,
            innungDict.GetValueOrDefault(o.OrganisationId, 0) +
            hwkDict.GetValueOrDefault(o.OrganisationId, 0),
            o.Gesperrt
        )).ToList();
    }

    public record OrganisationZeile(
        int OrganisationId, string OrganisationsNr, byte Typ,
        string Name, string? Kurzbezeichnung, string? Ort, string? Email,
        bool Sammelrechnung, int AnzahlBetriebe, bool Gesperrt);
}
