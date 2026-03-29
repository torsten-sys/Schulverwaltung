using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Schulverwaltung.Web.Pages.Organisationen;

public class KarteModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public KarteModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty] public OrganisationFormular Form { get; set; } = new();

    public bool   IstNeu  => Form.OrganisationId == 0;
    public string Titel   => IstNeu ? "Neue Organisation" : $"{Form.OrganisationsNr} · {Form.Name}";
    public string TypLabel => Form.OrganisationsTyp == 0 ? "INNUNG" : "HWK";

    public List<OrganisationBetriebZeile>         OrganisationsBetriebe { get; set; } = [];
    public List<OrganisationAenderungspostenZeile> Aenderungsposten      { get; set; } = [];
    public List<OrganisationAnhangZeile>           Anhaenge              { get; set; } = [];
    public OrganisationStatistik                   Statistik             { get; set; } = new();

    // ── GET ───────────────────────────────────────────────────────────────────

    public async Task<IActionResult> OnGetAsync(int? id, byte? typ)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle == 0)
            return RedirectToPage("/Zugriff/KeinZugriff");

        if (id == null || id == 0)
        {
            Form = new OrganisationFormular
            {
                Land           = "Deutschland",
                OrganisationsTyp = typ ?? 0
            };
            return Page();
        }

        var o = await _db.Organisationen.FirstOrDefaultAsync(x => x.OrganisationId == id);
        if (o == null) return NotFound();

        Form = MapToForm(o);

        await LadeBetriebe(o.OrganisationId);
        await LadeAenderungsposten(o.OrganisationId);
        await LadeAnhaenge(o.OrganisationId);
        await LadeStatistik(o.OrganisationId);

        return Page();
    }

    // ── GET: Anhang ausliefern ────────────────────────────────────────────────

    public async Task<IActionResult> OnGetAnhangAsync(int id)
    {
        var a = await _db.Anhaenge.FindAsync(id);
        if (a == null) return NotFound();
        return File(a.Inhalt, a.DateiTyp, a.DateiName);
    }

    // ── POST: Speichern ───────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostSpeichernAsync()
    {
        if (!ModelState.IsValid)
        {
            if (Form.OrganisationId != 0)
            {
                await LadeBetriebe(Form.OrganisationId);
                await LadeAenderungsposten(Form.OrganisationId);
                await LadeAnhaenge(Form.OrganisationId);
                await LadeStatistik(Form.OrganisationId);
            }
            return Page();
        }

        var user = User.Identity?.Name ?? "System";

        if (Form.OrganisationId == 0)
        {
            var nr = await NextNoAsync("ORG");
            var org = new Organisation
            {
                OrganisationsNr                 = nr,
                OrganisationsTyp                = Form.OrganisationsTyp,
                Name                            = Form.Name,
                Kurzbezeichnung                 = Form.Kurzbezeichnung,
                Strasse                         = Form.Strasse,
                PLZ                             = Form.PLZ,
                Ort                             = Form.Ort,
                Land                            = Form.Land ?? "Deutschland",
                Telefon                         = Form.Telefon,
                Email                           = Form.Email,
                Website                         = Form.Website,
                VereinbarteUebernachtungskosten = Form.VereinbarteUebernachtungskosten,
                Sammelrechnung                  = Form.Sammelrechnung,
                Gesperrt                        = Form.Gesperrt,
                Notiz                           = Form.Notiz
            };
            _db.Organisationen.Add(org);
            await _db.SaveChangesAsync();

            var belegNr = await NextNoAsync("AENDERUNG");
            AenderungspostenSchreiben(belegNr, org.OrganisationId, org.OrganisationsNr, org.Name,
                "OrganisationAngelegt", "Organisation", null, null, org.OrganisationsNr, user);
            await _db.SaveChangesAsync();

            return RedirectToPage(new { id = org.OrganisationId });
        }
        else
        {
            var o = await _db.Organisationen
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrganisationId == Form.OrganisationId);
            if (o == null) return NotFound();

            var tracked = await _db.Organisationen.FindAsync(Form.OrganisationId);
            if (tracked == null) return NotFound();

            var belegNr = await NextNoAsync("AENDERUNG");

            void Check(string feld, string? alt, string? neu)
            {
                if (string.Equals(alt?.Trim(), neu?.Trim(), StringComparison.OrdinalIgnoreCase)) return;
                AenderungspostenSchreiben(belegNr, o.OrganisationId, o.OrganisationsNr, o.Name,
                    "StammdatenGeaendert", "Organisation", feld, alt, neu, user);
            }
            void CheckBool(string feld, bool alt, bool neu)
            {
                if (alt == neu) return;
                AenderungspostenSchreiben(belegNr, o.OrganisationId, o.OrganisationsNr, o.Name,
                    "StammdatenGeaendert", "Organisation", feld,
                    alt ? "Ja" : "Nein", neu ? "Ja" : "Nein", user);
            }
            void CheckDecimal(string feld, decimal? alt, decimal? neu)
            {
                if (alt == neu) return;
                var de = new CultureInfo("de-DE");
                var altStr = alt.HasValue ? alt.Value.ToString("N2", de) + " €" : null;
                var neuStr = neu.HasValue ? neu.Value.ToString("N2", de) + " €" : null;
                AenderungspostenSchreiben(belegNr, o.OrganisationId, o.OrganisationsNr, o.Name,
                    "StammdatenGeaendert", "Organisation", feld, altStr, neuStr, user);
            }

            Check("Name",            o.Name,            Form.Name);
            Check("Kurzbezeichnung", o.Kurzbezeichnung, Form.Kurzbezeichnung);
            Check("Straße",          o.Strasse,         Form.Strasse);
            Check("PLZ",             o.PLZ,             Form.PLZ);
            Check("Ort",             o.Ort,             Form.Ort);
            Check("Land",            o.Land,            Form.Land);
            Check("Telefon",         o.Telefon,         Form.Telefon);
            Check("E-Mail",          o.Email,           Form.Email);
            Check("Website",         o.Website,         Form.Website);
            Check("Notiz",           o.Notiz,           Form.Notiz);
            CheckDecimal("Vereinbarte Übernachtungskosten",
                o.VereinbarteUebernachtungskosten,
                Form.VereinbarteUebernachtungskosten);
            CheckBool("Sammelrechnung", o.Sammelrechnung, Form.Sammelrechnung);

            if (o.Gesperrt != Form.Gesperrt)
            {
                var ereignis = Form.Gesperrt ? "Gesperrt" : "Entsperrt";
                AenderungspostenSchreiben(belegNr, o.OrganisationId, o.OrganisationsNr, o.Name,
                    ereignis, "Organisation", "Gesperrt",
                    o.Gesperrt ? "Ja" : "Nein", Form.Gesperrt ? "Ja" : "Nein", user);
            }

            // OrganisationsTyp ist nach erstem Speichern nicht änderbar – aus Form ignorieren
            tracked.Name                            = Form.Name;
            tracked.Kurzbezeichnung                 = Form.Kurzbezeichnung;
            tracked.Strasse                         = Form.Strasse;
            tracked.PLZ                             = Form.PLZ;
            tracked.Ort                             = Form.Ort;
            tracked.Land                            = Form.Land ?? "Deutschland";
            tracked.Telefon                         = Form.Telefon;
            tracked.Email                           = Form.Email;
            tracked.Website                         = Form.Website;
            tracked.VereinbarteUebernachtungskosten = Form.VereinbarteUebernachtungskosten;
            tracked.Sammelrechnung                  = Form.Sammelrechnung;
            tracked.Gesperrt                        = Form.Gesperrt;
            tracked.Notiz                           = Form.Notiz;

            await _db.SaveChangesAsync();
            return RedirectToPage(new { id = tracked.OrganisationId });
        }
    }

    // ── POST: Löschen ─────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostLoeschenAsync(int id)
    {
        var o = await _db.Organisationen.FirstOrDefaultAsync(x => x.OrganisationId == id);
        if (o == null) return NotFound();

        var hatBetriebe = await _db.Betriebe
            .AnyAsync(b => b.InnungsId == id || b.HandwerkskammerId == id);
        if (hatBetriebe)
        {
            ModelState.AddModelError("", "Organisation kann nicht gelöscht werden, da noch Betriebe zugeordnet sind.");
            Form = MapToForm(o);
            await LadeBetriebe(id);
            await LadeAenderungsposten(id);
            await LadeAnhaenge(id);
            await LadeStatistik(id);
            return Page();
        }

        _db.Organisationen.Remove(o);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    // ── POST: Anhang hochladen ────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAnhangHochladenAsync(
        int organisationId, string bezeichnung, IFormFile? datei)
    {
        if (datei == null || datei.Length == 0 || string.IsNullOrWhiteSpace(bezeichnung))
            return RedirectToPage(new { id = organisationId });

        if (datei.Length > 10 * 1024 * 1024)
        {
            ModelState.AddModelError("", "Datei darf max. 10 MB groß sein.");
            return RedirectToPage(new { id = organisationId });
        }

        using var ms = new MemoryStream();
        await datei.CopyToAsync(ms);

        var anhang = new Anhang
        {
            BezugTyp       = "Organisation",
            BezugId        = organisationId,
            Bezeichnung    = bezeichnung,
            DateiName      = datei.FileName,
            DateiTyp       = datei.ContentType,
            DateiGroesse   = (int)datei.Length,
            Inhalt         = ms.ToArray(),
            HochgeladenVon = User.Identity?.Name,
            HochgeladenAm  = DateTime.UtcNow
        };
        _db.Anhaenge.Add(anhang);
        await _db.SaveChangesAsync();

        return RedirectToPage(new { id = organisationId });
    }

    // ── POST: Anhang löschen ──────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAnhangLoeschenAsync(int anhangId, int organisationId)
    {
        var a = await _db.Anhaenge.FindAsync(anhangId);
        if (a != null)
        {
            _db.Anhaenge.Remove(a);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { id = organisationId });
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private async Task LadeBetriebe(int orgId)
    {
        var betriebe = await _db.Betriebe
            .Where(b => b.InnungsId == orgId || b.HandwerkskammerId == orgId)
            .OrderBy(b => b.Name)
            .ToListAsync();

        OrganisationsBetriebe = betriebe.Select(b => new OrganisationBetriebZeile(
            b.BetriebId, b.BetriebNr, b.Name, b.Ort, b.Telefon,
            b.InnungsId == orgId, b.HandwerkskammerId == orgId
        )).ToList();
    }

    private async Task LadeAenderungsposten(int orgId)
    {
        var posten = await _db.OrganisationAenderungsposten
            .Where(p => p.OrganisationId == orgId)
            .OrderByDescending(p => p.Zeitstempel)
            .ToListAsync();

        Aenderungsposten = posten.Select(p => new OrganisationAenderungspostenZeile(
            p.PostenId, p.BelegNr, p.Ereignis, p.Tabelle, p.Feld,
            p.AlterWert, p.NeuerWert, p.Zeitstempel, p.AusfuehrendUser, p.Notiz
        )).ToList();
    }

    private async Task LadeAnhaenge(int orgId)
    {
        var liste = await _db.Anhaenge
            .Where(a => a.BezugTyp == "Organisation" && a.BezugId == orgId)
            .OrderByDescending(a => a.HochgeladenAm)
            .ToListAsync();

        Anhaenge = liste.Select(a => new OrganisationAnhangZeile(
            a.AnhangId, a.Bezeichnung, a.DateiName, a.DateiTyp,
            a.DateiGroesse, a.HochgeladenVon, a.HochgeladenAm
        )).ToList();
    }

    private async Task LadeStatistik(int orgId)
    {
        var betriebe = await _db.Betriebe
            .Where(b => b.InnungsId == orgId || b.HandwerkskammerId == orgId)
            .Select(b => b.Gesperrt)
            .ToListAsync();

        Statistik = new OrganisationStatistik
        {
            AnzahlBetriebe       = betriebe.Count,
            AnzahlAktiveBetriebe = betriebe.Count(g => !g)
        };
    }

    private void AenderungspostenSchreiben(
        string belegNr, int orgId, string orgNr, string orgName,
        string ereignis, string? tabelle, string? feld,
        string? alterWert, string? neuerWert, string user)
    {
        _db.OrganisationAenderungsposten.Add(new OrganisationAenderungsposten
        {
            BelegNr           = belegNr,
            OrganisationId    = orgId,
            OrganisationsNr   = orgNr,
            OrganisationsName = orgName,
            Ereignis          = ereignis,
            Tabelle           = tabelle,
            Feld              = feld,
            AlterWert         = alterWert,
            NeuerWert         = neuerWert,
            Zeitstempel       = DateTime.UtcNow,
            AusfuehrendUser   = user
        });
    }

    private static OrganisationFormular MapToForm(Organisation o) => new()
    {
        OrganisationId                  = o.OrganisationId,
        OrganisationsNr                 = o.OrganisationsNr,
        OrganisationsTyp                = o.OrganisationsTyp,
        Name                            = o.Name,
        Kurzbezeichnung                 = o.Kurzbezeichnung,
        Strasse                         = o.Strasse,
        PLZ                             = o.PLZ,
        Ort                             = o.Ort,
        Land                            = o.Land,
        Telefon                         = o.Telefon,
        Email                           = o.Email,
        Website                         = o.Website,
        VereinbarteUebernachtungskosten = o.VereinbarteUebernachtungskosten,
        Sammelrechnung                  = o.Sammelrechnung,
        Gesperrt                        = o.Gesperrt,
        Notiz                           = o.Notiz
    };

    private async Task<string> NextNoAsync(string serieCode)
    {
        var zeile = await _db.NoSerieZeilen
            .Where(z => z.NoSerieCode == serieCode && z.Offen)
            .FirstOrDefaultAsync();

        if (zeile == null)
            throw new InvalidOperationException($"Keine offene Nummernserie für '{serieCode}'.");

        long naechste = zeile.LastNoUsed == null
            ? long.Parse(zeile.StartingNo.Substring(zeile.Prefix?.Length ?? 0))
            : long.Parse(zeile.LastNoUsed.Substring(zeile.Prefix?.Length ?? 0)) + zeile.IncrementBy;

        var nr = (zeile.Prefix ?? "") + naechste.ToString().PadLeft(zeile.NummerLaenge, '0');
        zeile.LastNoUsed   = nr;
        zeile.LastDateUsed = DateOnly.FromDateTime(DateTime.Today);
        return nr;
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record OrganisationBetriebZeile(
    int BetriebId, string BetriebNr, string Name,
    string? Ort, string? Telefon, bool IstInnung, bool IstKammer);

public record OrganisationAenderungspostenZeile(
    int PostenId, string BelegNr, string Ereignis, string? Tabelle,
    string? Feld, string? AlterWert, string? NeuerWert,
    DateTime Zeitstempel, string User, string? Notiz);

public record OrganisationAnhangZeile(
    int AnhangId, string Bezeichnung, string DateiName, string DateiTyp,
    int DateiGroesse, string? HochgeladenVon, DateTime HochgeladenAm);

public class OrganisationStatistik
{
    public int AnzahlBetriebe       { get; set; }
    public int AnzahlAktiveBetriebe { get; set; }
}

public class OrganisationFormular
{
    public int      OrganisationId                  { get; set; }
    public string   OrganisationsNr                 { get; set; } = "";
    public byte     OrganisationsTyp                { get; set; }
    [Required(ErrorMessage = "Name ist erforderlich")]
    public string   Name                            { get; set; } = "";
    public string?  Kurzbezeichnung                 { get; set; }
    public string?  Strasse                         { get; set; }
    public string?  PLZ                             { get; set; }
    public string?  Ort                             { get; set; }
    public string?  Land                            { get; set; }
    public string?  Telefon                         { get; set; }
    public string?  Email                           { get; set; }
    public string?  Website                         { get; set; }
    public decimal? VereinbarteUebernachtungskosten { get; set; }
    public bool     Sammelrechnung                  { get; set; }
    public bool     Gesperrt                        { get; set; }
    public string?  Notiz                           { get; set; }
}
