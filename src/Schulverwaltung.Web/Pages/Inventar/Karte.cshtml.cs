using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Pages.Inventar;

public class KarteModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    private readonly InventarService         _inv;
    public KarteModel(SchulverwaltungDbContext db, InventarService inv)
    { _db = db; _inv = inv; }

    [BindProperty] public InventarFormular Form { get; set; } = new();
    [BindProperty] public List<KomponenteFormular> KomponentenFormular { get; set; } = [];

    public bool   IstNeu => Form.InventarId == 0;
    public string Titel  => IstNeu
        ? "Neuer Inventargegenstand"
        : $"{Form.InventarNr} · {Form.Bezeichnung}{(Form.Typ != null ? " – " + Form.Typ : "")}";

    public List<InventarKategorie>  KategorienListe     { get; set; } = [];
    public List<Schulverwaltung.Domain.Entities.Raum> RaeumeListe { get; set; } = [];
    public List<PersonAuswahl>      PersonenListe       { get; set; } = [];
    public List<OrgEinheitAuswahl>  OrgEinheitenListe   { get; set; } = [];
    public List<BetriebAuswahl>     BetriebeListeWartung { get; set; } = [];
    public DateOnly?                NaechsteWartung     { get; set; }
    public List<ProtokollZeile>     Protokoll           { get; set; } = [];
    public List<KomponenteZeile>    Komponenten         { get; set; } = [];
    public List<WartungZeile>       Wartungen           { get; set; } = [];
    public List<AnhangZeile>        Anhaenge            { get; set; } = [];
    public string?                  Fehler              { get; set; }
    public string?                  Meldung             { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        bool darfInventar = benutzer != null && (benutzer.AppRolle == 3 || (benutzer.AppRolle >= 1 && benutzer.DarfInventarVerwalten));
        if (!darfInventar) return RedirectToPage("/Zugriff/KeinZugriff");

        Meldung = TempData["Meldung"] as string;
        Fehler  = TempData["Fehler"]  as string;

        await LadeAuswahllisteAsync();

        if (id == null || id == 0) return Page();

        var inv = await _db.Inventar
            .Include(i => i.Kategorie)
            .Include(i => i.Raum)
            .Include(i => i.Person)
            .Include(i => i.OrgEinheit)
            .FirstOrDefaultAsync(i => i.InventarId == id);
        if (inv == null) return NotFound();

        Form = new InventarFormular {
            InventarId              = inv.InventarId,
            InventarNr              = inv.InventarNr,
            Bezeichnung             = inv.Bezeichnung,
            Typ                     = inv.Typ,
            KategorieId             = inv.KategorieId,
            Seriennummer            = inv.Seriennummer,
            Anschaffungsdatum       = inv.Anschaffungsdatum,
            Anschaffungskosten      = inv.Anschaffungskosten,
            RaumId                  = inv.RaumId,
            PersonId                = inv.PersonId,
            OrgEinheitId            = inv.OrgEinheitId,
            Zustand                 = inv.Zustand,
            WartungStartdatum       = inv.WartungStartdatum,
            WartungIntervallMonate  = inv.WartungIntervallMonate,
            WartungLetztesDatum     = inv.WartungLetztesDatum,
            WartungNaechstesDatum   = inv.WartungNaechstesDatum,
            Gesperrt                = inv.Gesperrt,
            SperrGrund              = inv.SperrGrund,
            Notiz                   = inv.Notiz
        };

        NaechsteWartung = inv.WartungNaechstesDatum ?? InventarService.NaechsteWartungBerechnen(inv);

        Protokoll = await _db.InventarAenderungsposten
            .Where(p => p.InventarId == id)
            .OrderByDescending(p => p.Zeitstempel)
            .Select(p => new ProtokollZeile(p.Zeitstempel, p.Ereignis ?? "", p.Feld, p.AlterWert, p.NeuerWert, p.AusfuehrendUser))
            .ToListAsync();

        Komponenten = await _db.InventarKomponenten
            .Where(k => k.InventarId == id)
            .OrderBy(k => k.Reihenfolge).ThenBy(k => k.KomponenteId)
            .Select(k => new KomponenteZeile(k.KomponenteId, k.Bezeichnung, k.Menge, k.Seriennummer, k.Notiz, k.Reihenfolge))
            .ToListAsync();

        Wartungen = await _db.InventarWartungen
            .Where(w => w.InventarId == id)
            .OrderByDescending(w => w.WartungsDatum)
            .Select(w => new WartungZeile(w.WartungId, w.WartungsDatum, w.IstExtern, w.BetriebName, w.Anmerkungen, w.AusfuehrendUser, w.ErstelltAm))
            .ToListAsync();

        Anhaenge = await _db.Anhaenge
            .Where(a => a.BezugTyp == "Inventar" && a.BezugId == id)
            .OrderByDescending(a => a.HochgeladenAm)
            .Select(a => new AnhangZeile(a.AnhangId, a.Bezeichnung, a.DateiName, a.DateiTyp, a.DateiGroesse, a.HochgeladenAm))
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostSpeichernAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        bool darfInventar = benutzer != null && (benutzer.AppRolle == 3 || (benutzer.AppRolle >= 1 && benutzer.DarfInventarVerwalten));
        if (!darfInventar) return RedirectToPage("/Zugriff/KeinZugriff");

        await LadeAuswahllisteAsync();

        if (string.IsNullOrWhiteSpace(Form.Bezeichnung)) { Fehler = "Bezeichnung ist erforderlich."; return Page(); }
        if (Form.KategorieId == 0) { Fehler = "Kategorie ist erforderlich."; return Page(); }

        var user = benutzer?.AdBenutzername ?? "unbekannt";

        if (Form.InventarId == 0)
        {
            var inv = new Schulverwaltung.Domain.Entities.Inventar {
                Bezeichnung            = Form.Bezeichnung.Trim(),
                Typ                    = Form.Typ?.Trim(),
                KategorieId            = Form.KategorieId,
                Seriennummer           = Form.Seriennummer?.Trim(),
                Anschaffungsdatum      = Form.Anschaffungsdatum,
                Anschaffungskosten     = Form.Anschaffungskosten,
                RaumId                 = Form.RaumId == 0 ? null : Form.RaumId,
                PersonId               = Form.PersonId == 0 ? null : Form.PersonId,
                OrgEinheitId           = Form.OrgEinheitId == 0 ? null : Form.OrgEinheitId,
                Zustand                = Form.Zustand,
                WartungStartdatum      = Form.WartungStartdatum,
                WartungIntervallMonate = Form.WartungIntervallMonate,
                WartungLetztesDatum    = Form.WartungLetztesDatum,
                WartungNaechstesDatum  = Form.WartungNaechstesDatum,
                Gesperrt               = Form.Gesperrt,
                SperrGrund             = Form.SperrGrund?.Trim(),
                Notiz                  = Form.Notiz?.Trim()
            };
            await _inv.InventarAnlegenAsync(inv, user);

            // Komponenten speichern
            var komps = KomponentenFormularToEntities();
            if (komps.Count > 0)
                await _inv.KomponentenSpeichernAsync(inv.InventarId, komps, user);

            return RedirectToPage(new { id = inv.InventarId });
        }
        else
        {
            var inv = await _db.Inventar.FindAsync(Form.InventarId);
            if (inv == null) return NotFound();

            var aenderungen = new List<(string Ereignis, string? Feld, string? Alt, string? Neu)>();

            if (inv.Zustand != Form.Zustand)
                aenderungen.Add(("ZustandGeaendert", "Zustand",
                    InventarService.ZustandText(inv.Zustand), InventarService.ZustandText(Form.Zustand)));

            var altRaumId = inv.RaumId;
            var neuRaumId = Form.RaumId == 0 ? (int?)null : Form.RaumId;
            if (altRaumId != neuRaumId)
                aenderungen.Add(("RaumZugeordnet", "RaumId",
                    altRaumId?.ToString(), neuRaumId?.ToString()));

            var altPersonId = inv.PersonId;
            var neuPersonId = Form.PersonId == 0 ? (int?)null : Form.PersonId;
            if (altPersonId != neuPersonId)
                aenderungen.Add(("PersonZugeordnet", "PersonId",
                    altPersonId?.ToString(), neuPersonId?.ToString()));

            var neuOrgId = Form.OrgEinheitId == 0 ? (int?)null : Form.OrgEinheitId;
            if (inv.OrgEinheitId != neuOrgId)
                aenderungen.Add(("OrgEinheitGeaendert", "OrgEinheitId",
                    inv.OrgEinheitId?.ToString(), neuOrgId?.ToString()));

            bool wartungGeaendert =
                inv.WartungStartdatum      != Form.WartungStartdatum      ||
                inv.WartungIntervallMonate != Form.WartungIntervallMonate  ||
                inv.WartungLetztesDatum    != Form.WartungLetztesDatum;
            if (wartungGeaendert)
                aenderungen.Add(("WartungAktualisiert", "Wartung", null, null));

            bool stammdatenGeaendert =
                inv.Bezeichnung        != Form.Bezeichnung.Trim()        ||
                inv.Typ                != Form.Typ?.Trim()               ||
                inv.KategorieId        != Form.KategorieId               ||
                inv.Seriennummer       != Form.Seriennummer?.Trim()      ||
                inv.Anschaffungsdatum  != Form.Anschaffungsdatum         ||
                inv.Anschaffungskosten != Form.Anschaffungskosten        ||
                inv.Gesperrt           != Form.Gesperrt                  ||
                inv.SperrGrund         != Form.SperrGrund?.Trim()        ||
                inv.Notiz              != Form.Notiz?.Trim();
            if (stammdatenGeaendert)
                aenderungen.Add(("StammdatenGeaendert", null, null, null));

            inv.Bezeichnung            = Form.Bezeichnung.Trim();
            inv.Typ                    = Form.Typ?.Trim();
            inv.KategorieId            = Form.KategorieId;
            inv.Seriennummer           = Form.Seriennummer?.Trim();
            inv.Anschaffungsdatum      = Form.Anschaffungsdatum;
            inv.Anschaffungskosten     = Form.Anschaffungskosten;
            inv.RaumId                 = neuRaumId;
            inv.PersonId               = neuPersonId;
            inv.OrgEinheitId           = neuOrgId;
            inv.Zustand                = Form.Zustand;
            inv.WartungStartdatum      = Form.WartungStartdatum;
            inv.WartungIntervallMonate = Form.WartungIntervallMonate;
            inv.WartungLetztesDatum    = Form.WartungLetztesDatum;
            inv.WartungNaechstesDatum  = Form.WartungNaechstesDatum;
            inv.Gesperrt               = Form.Gesperrt;
            inv.SperrGrund             = Form.SperrGrund?.Trim();
            inv.Notiz                  = Form.Notiz?.Trim();

            await _db.SaveChangesAsync();

            foreach (var (ereignis, feld, alt, neu) in aenderungen)
                await _inv.AenderungspostenAsync(inv.InventarId, inv.InventarNr, inv.Bezeichnung,
                    ereignis, feld, alt, neu, user);

            // Komponenten speichern
            var komps = KomponentenFormularToEntities();
            await _inv.KomponentenSpeichernAsync(inv.InventarId, komps, user);

            return RedirectToPage(new { id = Form.InventarId });
        }
    }

    public async Task<IActionResult> OnPostLoeschenAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 3) return RedirectToPage("/Zugriff/KeinZugriff");
        var inv = await _db.Inventar.FindAsync(Form.InventarId);
        if (inv == null) return RedirectToPage("/Inventar/Index");
        _db.Inventar.Remove(inv);
        await _db.SaveChangesAsync();
        return RedirectToPage("/Inventar/Index");
    }

    public async Task<IActionResult> OnPostWartungAsync(DateOnly wartungsDatum, bool istExtern, int? betriebId, string? anmerkungen)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        bool darfInventar = benutzer != null && (benutzer.AppRolle == 3 || (benutzer.AppRolle >= 1 && benutzer.DarfInventarVerwalten));
        if (!darfInventar) return RedirectToPage("/Zugriff/KeinZugriff");

        if (Form.InventarId == 0) return RedirectToPage("/Inventar/Index");

        var user = benutzer?.AdBenutzername ?? "unbekannt";

        var wartung = new InventarWartung {
            WartungsDatum = wartungsDatum,
            IstExtern     = istExtern,
            BetriebId     = istExtern ? betriebId : null,
            Anmerkungen   = anmerkungen?.Trim()
        };

        try
        {
            await _inv.WartungDurchfuehrenAsync(Form.InventarId, wartung, user);
            TempData["Meldung"] = $"Wartung vom {wartungsDatum:dd.MM.yyyy} wurde eingetragen.";
        }
        catch (Exception ex)
        {
            TempData["Fehler"] = $"Fehler: {ex.Message}";
        }

        return RedirectToPage(new { id = Form.InventarId });
    }

    public async Task<IActionResult> OnGetAnhangAsync(int anhangId)
    {
        var anhang = await _db.Anhaenge.FindAsync(anhangId);
        if (anhang == null) return NotFound();
        return File(anhang.Inhalt, anhang.DateiTyp, anhang.DateiName);
    }

    public async Task<IActionResult> OnPostAnhangUploadAsync(IFormFile datei, string bezeichnung, int inventarId)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        bool darfInventar = benutzer != null && (benutzer.AppRolle == 3 || (benutzer.AppRolle >= 1 && benutzer.DarfInventarVerwalten));
        if (!darfInventar) return RedirectToPage("/Zugriff/KeinZugriff");

        if (datei == null || datei.Length == 0 || inventarId == 0)
        {
            TempData["Fehler"] = "Keine Datei oder kein Inventar angegeben.";
            return RedirectToPage(new { id = inventarId });
        }

        using var ms = new MemoryStream();
        await datei.CopyToAsync(ms);

        var anhang = new Anhang {
            BezugTyp       = "Inventar",
            BezugId        = inventarId,
            Bezeichnung    = string.IsNullOrWhiteSpace(bezeichnung) ? datei.FileName : bezeichnung.Trim(),
            DateiName      = datei.FileName,
            DateiTyp       = datei.ContentType,
            DateiGroesse   = (int)datei.Length,
            Inhalt         = ms.ToArray(),
            HochgeladenVon = benutzer?.AdBenutzername,
            HochgeladenAm  = DateTime.UtcNow
        };

        _db.Anhaenge.Add(anhang);
        await _db.SaveChangesAsync();

        TempData["Meldung"] = $"Anhang '{anhang.Bezeichnung}' hochgeladen.";
        return RedirectToPage(new { id = inventarId });
    }

    public async Task<IActionResult> OnPostAnhangLoeschenAsync(int anhangId, int inventarId)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        bool darfInventar = benutzer != null && (benutzer.AppRolle == 3 || (benutzer.AppRolle >= 1 && benutzer.DarfInventarVerwalten));
        if (!darfInventar) return RedirectToPage("/Zugriff/KeinZugriff");

        var anhang = await _db.Anhaenge.FindAsync(anhangId);
        if (anhang != null)
        {
            _db.Anhaenge.Remove(anhang);
            await _db.SaveChangesAsync();
            TempData["Meldung"] = "Anhang gelöscht.";
        }
        return RedirectToPage(new { id = inventarId });
    }

    private async Task LadeAuswahllisteAsync()
    {
        KategorienListe = await _db.InventarKategorien
            .Where(k => !k.Gesperrt)
            .OrderBy(k => k.Reihenfolge).ThenBy(k => k.Bezeichnung)
            .ToListAsync();

        RaeumeListe = await _db.Raeume
            .Where(r => !r.Gesperrt)
            .OrderBy(r => r.RaumNr)
            .ToListAsync();

        PersonenListe = await _db.Personen
            .Where(p => !p.Gesperrt)
            .OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .Select(p => new PersonAuswahl(p.PersonId, p.PersonNr, p.Nachname, p.Vorname))
            .ToListAsync();

        OrgEinheitenListe = await _db.Organisationseinheiten
            .Where(o => !o.Gesperrt)
            .OrderBy(o => o.Reihenfolge).ThenBy(o => o.Bezeichnung)
            .Select(o => new OrgEinheitAuswahl(o.OrgEinheitId, o.Code, o.Bezeichnung))
            .ToListAsync();

        BetriebeListeWartung = await _db.Betriebe
            .Where(b => !b.Gesperrt)
            .OrderBy(b => b.Name)
            .Select(b => new BetriebAuswahl(b.BetriebId, b.BetriebNr, b.Name))
            .ToListAsync();
    }

    private List<InventarKomponente> KomponentenFormularToEntities()
    {
        return KomponentenFormular
            .Where(k => !string.IsNullOrWhiteSpace(k.Bezeichnung))
            .Select(k => new InventarKomponente {
                KomponenteId = k.KomponenteId,
                Bezeichnung  = k.Bezeichnung.Trim(),
                Menge        = k.Menge,
                Seriennummer = k.Seriennummer?.Trim(),
                Notiz        = k.Notiz?.Trim(),
                Reihenfolge  = k.Reihenfolge
            })
            .ToList();
    }

    public record PersonAuswahl(int PersonId, string PersonNr, string Nachname, string Vorname);
    public record ProtokollZeile(DateTime Zeitstempel, string Ereignis, string? Feld, string? AlterWert, string? NeuerWert, string AusfuehrendUser);
    public record KomponenteZeile(int KomponenteId, string Bezeichnung, decimal Menge, string? Seriennummer, string? Notiz, int Reihenfolge);
    public record OrgEinheitAuswahl(int OrgEinheitId, string Code, string Bezeichnung);
    public record BetriebAuswahl(int BetriebId, string BetriebNr, string Name);
    public record AnhangZeile(int AnhangId, string Bezeichnung, string DateiName, string DateiTyp, int DateiGroesse, DateTime HochgeladenAm);
    public record WartungZeile(int WartungId, DateOnly WartungsDatum, bool IstExtern, string? BetriebName, string? Anmerkungen, string AusfuehrendUser, DateTime ErstelltAm);

    public class KomponenteFormular
    {
        public int      KomponenteId { get; set; }
        public string   Bezeichnung  { get; set; } = "";
        public decimal  Menge        { get; set; } = 1m;
        public string?  Seriennummer { get; set; }
        public string?  Notiz        { get; set; }
        public int      Reihenfolge  { get; set; }
    }

    public class WartungFormular
    {
        public DateOnly WartungsDatum { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public bool     IstExtern     { get; set; }
        public int?     BetriebId     { get; set; }
        public string?  Anmerkungen   { get; set; }
    }

    public class InventarFormular
    {
        public int      InventarId             { get; set; }
        public string   InventarNr             { get; set; } = "";
        public string   Bezeichnung            { get; set; } = "";
        public string?  Typ                    { get; set; }
        public int      KategorieId            { get; set; }
        public string?  Seriennummer           { get; set; }
        public DateOnly? Anschaffungsdatum     { get; set; }
        public decimal? Anschaffungskosten     { get; set; }
        public int?     RaumId                 { get; set; }
        public int?     PersonId               { get; set; }
        public int?     OrgEinheitId           { get; set; }
        public byte     Zustand                { get; set; } = 0;
        public DateOnly? WartungStartdatum     { get; set; }
        public int?     WartungIntervallMonate { get; set; }
        public DateOnly? WartungLetztesDatum   { get; set; }
        public DateOnly? WartungNaechstesDatum { get; set; }
        public bool     Gesperrt               { get; set; }
        public string?  SperrGrund             { get; set; }
        public string?  Notiz                  { get; set; }
    }
}
