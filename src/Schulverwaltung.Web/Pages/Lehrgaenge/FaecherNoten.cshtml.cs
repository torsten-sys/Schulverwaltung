using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class FaecherNotenModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public FaecherNotenModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public int    LehrgangId { get; set; }
    public string LehrgangNr { get; set; } = "";
    public string Fehler     { get; set; } = "";

    public List<FachZeile>                        Faecher { get; set; } = [];
    public Dictionary<int, NotenPersonZeile>      Noten   { get; set; } = [];

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

    public async Task<IActionResult> OnPostFachSpeichernAsync(
        int fachId, int lehrgangId, string bezeichnung, decimal gewichtung, int reihenfolge)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 1)
            return RedirectToPage("/Zugriff/KeinZugriff");

        if (string.IsNullOrWhiteSpace(bezeichnung))
        {
            Fehler = "Bezeichnung darf nicht leer sein.";
            Id = lehrgangId;
            var l = await _db.Lehrgaenge.FindAsync(lehrgangId);
            LehrgangId = lehrgangId;
            LehrgangNr = l?.LehrgangNr ?? "";
            await LadeDaten();
            return Page();
        }

        if (fachId == 0)
        {
            _db.MeisterFaecher.Add(new MeisterFach
            {
                LehrgangId  = lehrgangId,
                Bezeichnung = bezeichnung.Trim(),
                Gewichtung  = gewichtung,
                Reihenfolge = reihenfolge
            });
        }
        else
        {
            var fach = await _db.MeisterFaecher.FindAsync(fachId);
            if (fach != null)
            {
                fach.Bezeichnung = bezeichnung.Trim();
                fach.Gewichtung  = gewichtung;
                fach.Reihenfolge = reihenfolge;
            }
        }
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = lehrgangId });
    }

    public async Task<IActionResult> OnPostFachLoeschenAsync(int fachId, int lehrgangId)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 1)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var fach = await _db.MeisterFaecher.FindAsync(fachId);
        if (fach != null) { _db.MeisterFaecher.Remove(fach); await _db.SaveChangesAsync(); }
        return RedirectToPage(new { id = lehrgangId });
    }

    private async Task LadeDaten()
    {
        var faecher = await _db.MeisterFaecher
            .Where(f => f.LehrgangId == LehrgangId)
            .OrderBy(f => f.Reihenfolge)
            .ToListAsync();

        Faecher = faecher.Select(f => new FachZeile(f.FachId, f.Bezeichnung, f.Gewichtung, f.Reihenfolge)).ToList();

        var noten = await _db.MeisterNoten
            .Where(n => n.LehrgangId == LehrgangId)
            .ToListAsync();

        var fachGewichtung = faecher.ToDictionary(f => f.FachId, f => f.Gewichtung);
        var personIds = noten.Select(n => n.PersonId).Distinct().ToList();

        Noten = [];
        foreach (var pid in personIds)
        {
            var pNoten  = noten.Where(n => n.PersonId == pid).ToList();
            var erste   = pNoten.FirstOrDefault();
            var fachNoten = faecher.ToDictionary(
                f => f.FachId,
                f => pNoten.FirstOrDefault(n => n.FachId == f.FachId));

            decimal? gesamt = null;
            var bewertete = pNoten.Where(n => n.Note.HasValue).ToList();
            if (bewertete.Count == faecher.Count && faecher.Count > 0)
            {
                var sum = bewertete.Sum(n =>
                    (decimal)n.Note!.Value * (fachGewichtung.TryGetValue(n.FachId, out var gw) ? gw : 1m));
                var gew = faecher.Sum(f => f.Gewichtung);
                if (gew > 0) gesamt = Math.Round(sum / gew, 1);
            }

            Noten[pid] = new NotenPersonZeile(
                pid, erste?.PersonNr ?? "", erste?.PersonName ?? "",
                fachNoten, gesamt);
        }
    }

    public record FachZeile(int FachId, string Bezeichnung, decimal Gewichtung, int Reihenfolge);

    public class NotenPersonZeile
    {
        public int PersonId   { get; }
        public string PersonNr   { get; }
        public string PersonName { get; }
        public Dictionary<int, MeisterNote?> FachNoten { get; }
        public decimal? Gesamtnote { get; }

        public NotenPersonZeile(int id, string nr, string name,
            Dictionary<int, MeisterNote?> fachNoten, decimal? gesamt)
        {
            PersonId   = id;
            PersonNr   = nr;
            PersonName = name;
            FachNoten  = fachNoten;
            Gesamtnote = gesamt;
        }
    }
}
