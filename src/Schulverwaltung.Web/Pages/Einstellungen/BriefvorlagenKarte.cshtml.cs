using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;
using System.Text.RegularExpressions;

namespace Schulverwaltung.Web.Pages.Einstellungen;

public class BriefvorlagenKarteModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    private readonly IWebHostEnvironment      _env;

    public BriefvorlagenKarteModel(SchulverwaltungDbContext db, IWebHostEnvironment env)
    {
        _db  = db;
        _env = env;
    }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public BriefvorlageForm Form { get; set; } = new();

    public bool             IstNeu { get; set; }
    public string?          Fehler { get; set; }
    public List<BriefBild>  Bilder { get; set; } = [];

    // Shared upload directory (global for all Briefvorlagen)
    private string UploadDir => Path.Combine(_env.WebRootPath, "uploads", "briefvorlagen");

    private static readonly HashSet<string> _erlaubteExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp" };

    // ── GET ──────────────────────────────────────────────────────────────────

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        if (Id.HasValue && Id > 0)
        {
            var vorlage = await _db.Briefvorlagen.FindAsync(Id.Value);
            if (vorlage == null) return NotFound();
            Form = new BriefvorlageForm {
                BriefvorlageId = vorlage.BriefvorlageId,
                Bezeichnung    = vorlage.Bezeichnung,
                KopfHtml       = vorlage.KopfHtml,
                FussHtml       = vorlage.FussHtml,
                IstStandard    = vorlage.IstStandard,
                Gesperrt       = vorlage.Gesperrt
            };
            IstNeu = false;
        }
        else
        {
            IstNeu = true;
        }

        LadeBilder();
        return Page();
    }

    // ── POST: Speichern ──────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostSpeichernAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        if (string.IsNullOrWhiteSpace(Form.Bezeichnung))
        {
            Fehler = "Bezeichnung darf nicht leer sein.";
            IstNeu = Form.BriefvorlageId == 0;
            LadeBilder();
            return Page();
        }

        if (Form.IstStandard)
        {
            var andere = await _db.Briefvorlagen
                .Where(v => v.IstStandard && v.BriefvorlageId != Form.BriefvorlageId)
                .ToListAsync();
            foreach (var v in andere) v.IstStandard = false;
        }

        if (Form.BriefvorlageId == 0)
        {
            var neu = new Briefvorlage {
                Bezeichnung = Form.Bezeichnung.Trim(),
                KopfHtml    = Form.KopfHtml,
                FussHtml    = Form.FussHtml,
                IstStandard = Form.IstStandard,
                Gesperrt    = Form.Gesperrt
            };
            _db.Briefvorlagen.Add(neu);
            await _db.SaveChangesAsync();
            TempData["Meldung"] = $"'{neu.Bezeichnung}' gespeichert.";
            return RedirectToPage(new { id = neu.BriefvorlageId });
        }
        else
        {
            var vorlage = await _db.Briefvorlagen.FindAsync(Form.BriefvorlageId);
            if (vorlage == null) return NotFound();
            vorlage.Bezeichnung = Form.Bezeichnung.Trim();
            vorlage.KopfHtml    = Form.KopfHtml;
            vorlage.FussHtml    = Form.FussHtml;
            vorlage.IstStandard = Form.IstStandard;
            vorlage.Gesperrt    = Form.Gesperrt;
            await _db.SaveChangesAsync();
            TempData["Meldung"] = $"'{vorlage.Bezeichnung}' gespeichert.";
            return RedirectToPage(new { id = vorlage.BriefvorlageId });
        }
    }

    // ── POST: Vorlage löschen ────────────────────────────────────────────────

    public async Task<IActionResult> OnPostLoeschenAsync(int id)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return RedirectToPage("/Zugriff/KeinZugriff");

        var vorlage = await _db.Briefvorlagen.FindAsync(id);
        if (vorlage != null)
        {
            _db.Briefvorlagen.Remove(vorlage);
            await _db.SaveChangesAsync();
            TempData["Meldung"] = $"'{vorlage.Bezeichnung}' gelöscht.";
        }
        return RedirectToPage("/Einstellungen/Briefvorlagen");
    }

    // ── POST: Bild hochladen (AJAX → JSON) ──────────────────────────────────

    public async Task<IActionResult> OnPostBildHochladenAsync(IFormFile datei)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return new JsonResult(new { fehler = "Kein Zugriff" }) { StatusCode = 403 };

        if (datei == null || datei.Length == 0)
            return new JsonResult(new { fehler = "Keine Datei empfangen." });

        var ext = Path.GetExtension(datei.FileName).ToLower();
        if (!_erlaubteExtensions.Contains(ext))
            return new JsonResult(new { fehler = $"Dateityp '{ext}' nicht erlaubt. Erlaubt: PNG, JPG, GIF, SVG, WebP." });

        if (datei.Length > 3 * 1024 * 1024)
            return new JsonResult(new { fehler = "Datei zu groß (max. 3 MB)." });

        // Dateiname: sanitize + kurze GUID-Endung
        var baseName = Path.GetFileNameWithoutExtension(datei.FileName);
        baseName = Regex.Replace(baseName, @"[^a-zA-Z0-9\-_äöüÄÖÜß]", "-");
        baseName = baseName.Trim('-');
        if (baseName.Length > 40) baseName = baseName.Substring(0, 40);
        if (string.IsNullOrEmpty(baseName)) baseName = "bild";
        var shortGuid = Guid.NewGuid().ToString("N")[..8];
        var dateiname = $"{baseName}-{shortGuid}{ext}";

        Directory.CreateDirectory(UploadDir);
        var pfad = Path.Combine(UploadDir, dateiname);

        await using var stream = System.IO.File.Create(pfad);
        await datei.CopyToAsync(stream);

        var url         = $"/uploads/briefvorlagen/{dateiname}";
        var anzeigename = $"{baseName}{ext}";
        var groesseKb   = (datei.Length / 1024.0).ToString("F0");

        return new JsonResult(new { url, dateiname, anzeigename, groesseKb });
    }

    // ── POST: Bild löschen (AJAX → JSON) ────────────────────────────────────

    public IActionResult OnPostBildLoeschen(string dateiname)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3)
            return new JsonResult(new { fehler = "Kein Zugriff" }) { StatusCode = 403 };

        // Sicherheit: nur einfacher Dateiname, keine Pfadanteile
        if (string.IsNullOrWhiteSpace(dateiname) || dateiname.Contains('/') || dateiname.Contains('\\'))
            return new JsonResult(new { fehler = "Ungültiger Dateiname." });

        var ext = Path.GetExtension(dateiname).ToLower();
        if (!_erlaubteExtensions.Contains(ext))
            return new JsonResult(new { fehler = "Unbekannter Dateityp." });

        var pfad = Path.Combine(UploadDir, dateiname);
        if (System.IO.File.Exists(pfad))
            System.IO.File.Delete(pfad);

        return new JsonResult(new { ok = true });
    }

    // ── Hilfsmethoden ────────────────────────────────────────────────────────

    private void LadeBilder()
    {
        if (!Directory.Exists(UploadDir)) return;

        Bilder = Directory.GetFiles(UploadDir)
            .Where(f => _erlaubteExtensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderByDescending(f => System.IO.File.GetLastWriteTime(f))
            .Select(f =>
            {
                var fname       = Path.GetFileName(f);
                var url         = $"/uploads/briefvorlagen/{fname}";
                var anzeigename = Regex.Replace(Path.GetFileNameWithoutExtension(fname), @"-[0-9a-f]{8}$", "")
                                  + Path.GetExtension(fname);
                var groesse     = new FileInfo(f).Length;
                return new BriefBild(fname, url, anzeigename, groesse);
            })
            .ToList();
    }

    // ── Typen ────────────────────────────────────────────────────────────────

    public record BriefBild(string Dateiname, string Url, string Anzeigename, long Groesse);

    public class BriefvorlageForm
    {
        public int     BriefvorlageId { get; set; }
        public string  Bezeichnung    { get; set; } = "";
        public string? KopfHtml       { get; set; }
        public string? FussHtml       { get; set; }
        public bool    IstStandard    { get; set; }
        public bool    Gesperrt       { get; set; }
    }
}
