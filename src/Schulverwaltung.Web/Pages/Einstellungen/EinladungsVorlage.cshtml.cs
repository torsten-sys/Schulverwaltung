using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Einstellungen;

public class EinladungsVorlageModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public EinladungsVorlageModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty] public EinladungsVorlage Form { get; set; } = new();
    public string? Meldung { get; set; }
    public string? Fehler  { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        Form = await _db.EinladungsVorlagen.FindAsync(1) ?? new EinladungsVorlage();
        if (TempData["Meldung"] is string m) Meldung = m;
        return Page();
    }

    public async Task<IActionResult> OnPostSpeichernAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var existing = await _db.EinladungsVorlagen.FindAsync(1);
        if (existing == null)
        {
            Form.EinladungsVorlageId = 1;
            _db.EinladungsVorlagen.Add(Form);
        }
        else
        {
            existing.Anschreiben       = Form.Anschreiben;
            existing.ZahlungsplanText  = Form.ZahlungsplanText;
            existing.InternatAbschnitt = Form.InternatAbschnitt;
            existing.RatenplanText     = Form.RatenplanText;
            existing.Schlusstext       = Form.Schlusstext;
        }
        await _db.SaveChangesAsync();
        TempData["Meldung"] = "Einladungsvorlage gespeichert.";
        return RedirectToPage();
    }
}
