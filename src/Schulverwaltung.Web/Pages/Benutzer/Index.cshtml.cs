using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Pages.Benutzer;

public class BenutzerIndexModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    private readonly AppBenutzerService      _service;
    public BenutzerIndexModel(SchulverwaltungDbContext db, AppBenutzerService service)
    { _db = db; _service = service; }

    [BindProperty(SupportsGet = true)] public string? Suche        { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterRolle  { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterStatus { get; set; }

    public List<BenutzerZeile> Benutzer       { get; set; } = [];
    public int                 OhneRolleAnzahl { get; set; }
    public string?             Fehler          { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        await LadeDaten();
        return Page();
    }

    public async Task<IActionResult> OnPostRolleAsync(int benutzerId, byte neueRolle)
    {
        var user = User.Identity?.Name ?? "System";
        try { await _service.RolleSetzenAsync(benutzerId, neueRolle, user); }
        catch (Exception ex) { Fehler = ex.Message; }
        await LadeDaten();
        return Page();
    }

    public async Task<IActionResult> OnPostPersonAsync(int benutzerId, int? personId)
    {
        var user = User.Identity?.Name ?? "System";
        try { await _service.PersonVerknuepfenAsync(benutzerId, personId == 0 ? null : personId, user); }
        catch (Exception ex) { Fehler = ex.Message; }
        await LadeDaten();
        return Page();
    }

    public async Task<IActionResult> OnPostSperrenAsync(int benutzerId, string? sperrGrund)
    {
        var user = User.Identity?.Name ?? "System";
        try { await _service.SperrenAsync(benutzerId, sperrGrund ?? "", user); }
        catch (Exception ex) { Fehler = ex.Message; }
        await LadeDaten();
        return Page();
    }

    public async Task<IActionResult> OnPostEntsperrenAsync(int benutzerId)
    {
        var user = User.Identity?.Name ?? "System";
        try { await _service.EntsperrenAsync(benutzerId, user); }
        catch (Exception ex) { Fehler = ex.Message; }
        await LadeDaten();
        return Page();
    }

    private async Task LadeDaten()
    {
        var query = _db.AppBenutzer.Include(b => b.Person).AsQueryable();

        if (!string.IsNullOrWhiteSpace(Suche))
        {
            var s = Suche.Trim().ToLower();
            query = query.Where(b =>
                b.AdBenutzername.ToLower().Contains(s) ||
                (b.DisplayName != null && b.DisplayName.ToLower().Contains(s)));
        }

        if (!string.IsNullOrEmpty(FilterRolle) && byte.TryParse(FilterRolle, out var rolle))
            query = query.Where(b => b.AppRolle == rolle);

        if (FilterStatus == "gesperrt")
            query = query.Where(b => b.Gesperrt);
        else if (FilterStatus == "aktiv")
            query = query.Where(b => !b.Gesperrt);

        var alle = await query.OrderBy(b => b.AdBenutzername).ToListAsync();

        OhneRolleAnzahl = await _db.AppBenutzer.CountAsync(b => b.AppRolle == 0 && !b.Gesperrt);

        var personenListe = await _db.Personen
            .OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .Select(p => new { p.PersonId, Name = p.Nachname + ", " + p.Vorname })
            .ToListAsync();

        Benutzer = alle.Select(b => new BenutzerZeile(
            b.BenutzerId, b.AdBenutzername, b.DisplayName, b.Email,
            b.AppRolle, AppBenutzerService.RolleText(b.AppRolle),
            b.PersonId, b.Person != null ? b.Person.Nachname + ", " + b.Person.Vorname : null,
            b.Gesperrt, b.SperrGrund, b.LetzterLogin,
            personenListe.Select(p => (p.PersonId, p.Name)).ToList()
        )).ToList();
    }
}

public record BenutzerZeile(
    int BenutzerId, string AdBenutzername, string? DisplayName, string? Email,
    byte AppRolle, string RolleText,
    int? PersonId, string? PersonName,
    bool Gesperrt, string? SperrGrund, DateTime? LetzterLogin,
    List<(int PersonId, string Name)> PersonenListe);
