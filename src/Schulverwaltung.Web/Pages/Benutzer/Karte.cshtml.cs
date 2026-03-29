using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Pages.Benutzer;

public class BenutzerKarteModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    private readonly AppBenutzerService      _service;
    public BenutzerKarteModel(SchulverwaltungDbContext db, AppBenutzerService service)
    { _db = db; _service = service; }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public AppBenutzer?                       Benutzer       { get; set; }
    public List<AppBenutzerAenderungsposten>  Protokoll      { get; set; } = [];
    public List<(int PersonId, string Name)>  PersonenListe  { get; set; } = [];
    public string?                            Fehler         { get; set; }

    public string Titel => Benutzer == null ? "Neuer Benutzer"
        : $"{Benutzer.AdBenutzername}{(Benutzer.DisplayName != null ? " · " + Benutzer.DisplayName : "")}";

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        await LadeDaten();
        return Page();
    }

    public async Task<IActionResult> OnPostSpeichernAsync(
        byte appRolle, int? personId, bool gesperrt, string? sperrGrund, string? notiz,
        bool darfInventarVerwalten)
    {
        var user = User.Identity?.Name ?? "System";
        var b = await _db.AppBenutzer.FindAsync(Id);
        if (b == null) return NotFound();

        // Rolle
        if (b.AppRolle != appRolle)
            await _service.RolleSetzenAsync(Id, appRolle, user);

        // Person
        var pId = personId == 0 ? null : personId;
        if (b.PersonId != pId)
            await _service.PersonVerknuepfenAsync(Id, pId, user);

        // Sperren/Entsperren
        if (gesperrt && !b.Gesperrt)
            await _service.SperrenAsync(Id, sperrGrund ?? "", user);
        else if (!gesperrt && b.Gesperrt)
            await _service.EntsperrenAsync(Id, user);

        // DarfInventarVerwalten
        if (b.DarfInventarVerwalten != darfInventarVerwalten)
            await _service.DarfInventarVerwaltenSetzenAsync(Id, darfInventarVerwalten, user);

        // Notiz
        await _service.NotizSpeichernAsync(Id, notiz);

        return RedirectToPage(new { id = Id });
    }

    private async Task LadeDaten()
    {
        Benutzer = await _db.AppBenutzer.Include(b => b.Person).FirstOrDefaultAsync(b => b.BenutzerId == Id);

        Protokoll = await _db.AppBenutzerAenderungsposten
            .Where(p => p.BenutzerId == Id)
            .OrderByDescending(p => p.Zeitstempel)
            .ToListAsync();

        PersonenListe = await _db.Personen
            .OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .Select(p => new { p.PersonId, Name = p.Nachname + ", " + p.Vorname })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(p => (p.PersonId, p.Name)).ToList());
    }
}
