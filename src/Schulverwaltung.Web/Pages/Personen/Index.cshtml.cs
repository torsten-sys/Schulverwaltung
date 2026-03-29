using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Personen;

public class IndexModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public IndexModel(SchulverwaltungDbContext db) => _db = db;

    public bool DarfBearbeiten =>
        HttpContext.Items["AppBenutzer"] is AppBenutzer b && b.AppRolle >= 1 && !b.Gesperrt;

    public IReadOnlyList<PersonZeile> Personen { get; set; } = [];
    public string? Suche       { get; set; }
    public string? RolleFilter { get; set; }

    public async Task OnGetAsync(string? suche, string? rolleFilter)
    {
        Suche       = suche;
        RolleFilter = rolleFilter;

        var query = _db.Personen
            .Include(p => p.Rollen.Where(r => r.Status == 0))
                .ThenInclude(r => r.Betrieb)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(suche))
        {
            var s = suche.Trim().ToLower();
            query = query.Where(p =>
                p.PersonNr.ToLower().Contains(s) ||
                p.Nachname.ToLower().Contains(s) ||
                p.Vorname.ToLower().Contains(s) ||
                (p.Email != null && p.Email.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(rolleFilter) && byte.TryParse(rolleFilter, out var rt))
        {
            var rolleTyp = (PersonRolleTyp)rt;
            query = query.Where(p => p.Rollen.Any(r => r.RolleTyp == rolleTyp && r.Status == 0));
        }

        var liste = await query
            .OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .ToListAsync();

        Personen = liste.Select(p => new PersonZeile(
            p.PersonId, p.PersonNr, p.Titel, p.Vorname, p.Nachname,
            p.Rollen.Where(r => r.Status == 0).Select(r => r.RolleTyp).ToList(),
            p.Rollen.Where(r => r.Status == 0 && r.Betrieb != null)
                    .Select(r => r.Betrieb!.Name).Distinct().ToList(),
            p.Ort, p.Email, p.Gesperrt
        )).ToList();
    }

    public record PersonZeile(
        int PersonId, string PersonNr, string? Titel, string Vorname, string Nachname,
        List<PersonRolleTyp> Rollen, List<string> Betriebe,
        string? Ort, string? Email, bool Gesperrt);
}
