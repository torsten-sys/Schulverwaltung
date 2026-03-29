using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Pages.Raeume;

public class KarteModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    private readonly InventarService         _inv;
    public KarteModel(SchulverwaltungDbContext db, InventarService inv)
    { _db = db; _inv = inv; }

    [BindProperty] public RaumFormular Form { get; set; } = new();

    public bool   IstNeu => Form.RaumId == 0;
    public string Titel  => IstNeu ? "Neuer Raum" : $"{Form.RaumNr} · {Form.Bezeichnung}";

    public List<RaumTyp>       RaumTypen    { get; set; } = [];
    public List<InventarZeile> Inventar     { get; set; } = [];
    public RaumTyp?            AktuellerTyp { get; set; }
    public string?             Fehler       { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        bool darfInventar = benutzer != null && (benutzer.AppRolle == 3 || (benutzer.AppRolle >= 1 && benutzer.DarfInventarVerwalten));
        if (!darfInventar) return RedirectToPage("/Zugriff/KeinZugriff");

        RaumTypen = await _db.RaumTypen.Where(t => !t.Gesperrt).OrderBy(t => t.Reihenfolge).ToListAsync();

        if (id == null || id == 0) return Page();

        var r = await _db.Raeume.Include(x => x.RaumTyp).FirstOrDefaultAsync(x => x.RaumId == id);
        if (r == null) return NotFound();

        Form = new RaumFormular {
            RaumId      = r.RaumId,
            RaumNr      = r.RaumNr,
            Bezeichnung = r.Bezeichnung,
            RaumTypId   = r.RaumTypId,
            Kapazitaet  = r.Kapazitaet,
            Gesperrt    = r.Gesperrt,
            SperrGrund  = r.SperrGrund,
            Notiz       = r.Notiz
        };
        AktuellerTyp = r.RaumTyp;

        Inventar = await _db.Inventar
            .Include(i => i.Kategorie)
            .Include(i => i.Person)
            .Where(i => i.RaumId == id)
            .OrderBy(i => i.InventarNr)
            .Select(i => new InventarZeile(
                i.InventarId, i.InventarNr, i.Bezeichnung,
                i.Kategorie.Bezeichnung, i.Zustand, InventarService.ZustandText(i.Zustand),
                i.Person != null ? i.Person.Nachname + ", " + i.Person.Vorname : null))
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostSpeichernAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        bool darfInventar = benutzer != null && (benutzer.AppRolle == 3 || (benutzer.AppRolle >= 1 && benutzer.DarfInventarVerwalten));
        if (!darfInventar) return RedirectToPage("/Zugriff/KeinZugriff");

        RaumTypen = await _db.RaumTypen.Where(t => !t.Gesperrt).OrderBy(t => t.Reihenfolge).ToListAsync();

        if (string.IsNullOrWhiteSpace(Form.Bezeichnung)) { Fehler = "Bezeichnung ist erforderlich."; return Page(); }
        if (Form.RaumTypId == 0) { Fehler = "Raumtyp ist erforderlich."; return Page(); }

        if (Form.RaumId == 0)
        {
            var raum = new Raum {
                Bezeichnung = Form.Bezeichnung.Trim(),
                RaumTypId   = Form.RaumTypId,
                Kapazitaet  = Form.Kapazitaet,
                Gesperrt    = Form.Gesperrt,
                SperrGrund  = Form.SperrGrund?.Trim(),
                Notiz       = Form.Notiz?.Trim()
            };
            await _inv.RaumAnlegenAsync(raum);
            return RedirectToPage(new { id = raum.RaumId });
        }
        else
        {
            var r = await _db.Raeume.FindAsync(Form.RaumId);
            if (r == null) return NotFound();
            r.Bezeichnung = Form.Bezeichnung.Trim();
            r.RaumTypId   = Form.RaumTypId;
            r.Kapazitaet  = Form.Kapazitaet;
            r.Gesperrt    = Form.Gesperrt;
            r.SperrGrund  = Form.SperrGrund?.Trim();
            r.Notiz       = Form.Notiz?.Trim();
            await _db.SaveChangesAsync();
            return RedirectToPage(new { id = Form.RaumId });
        }
    }

    public async Task<IActionResult> OnPostLoeschenAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3) return RedirectToPage("/Zugriff/KeinZugriff");
        var r = await _db.Raeume.FindAsync(Form.RaumId);
        if (r == null) return RedirectToPage("/Raeume/Index");
        if (await _db.Inventar.AnyAsync(i => i.RaumId == Form.RaumId))
        {
            RaumTypen = await _db.RaumTypen.Where(t => !t.Gesperrt).OrderBy(t => t.Reihenfolge).ToListAsync();
            Fehler = "Raum hat Inventar – bitte zuerst Inventar umlagern.";
            return Page();
        }
        _db.Raeume.Remove(r);
        await _db.SaveChangesAsync();
        return RedirectToPage("/Raeume/Index");
    }

    public record InventarZeile(int InventarId, string InventarNr, string Bezeichnung,
        string Kategorie, byte Zustand, string ZustandText, string? PersonName);

    public class RaumFormular
    {
        public int     RaumId      { get; set; }
        public string  RaumNr      { get; set; } = "";
        public string  Bezeichnung { get; set; } = "";
        public int     RaumTypId   { get; set; }
        public int?    Kapazitaet  { get; set; }
        public bool    Gesperrt    { get; set; }
        public string? SperrGrund  { get; set; }
        public string? Notiz       { get; set; }
    }
}
