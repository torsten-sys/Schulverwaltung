using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class KarteModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    private readonly MeisterkursService       _mk;
    public KarteModel(SchulverwaltungDbContext db, MeisterkursService mk)
    {
        _db = db;
        _mk = mk;
    }

    [BindProperty] public LehrgangFormular    Form        { get; set; } = new();
    [BindProperty] public EinheitFormular     EinheitForm { get; set; } = new();

    public bool   IstNeu          => Form.LehrgangId == 0;
    public string Titel           => IstNeu ? "Neuer Lehrgang" : $"{Form.LehrgangNr} · {Form.Bezeichnung}";
    public bool   ZeigeEinheiten  => Form.LehrgangTyp != 0;
    public bool   ZeigeMeisterTabs => Form.LehrgangTyp == 0;

    public List<LehrgangArt>                LehrgangArten       { get; set; } = [];
    public List<DozentenAuswahl>            DozentenListe       { get; set; } = [];
    public List<LehrgangPersonZeile>        ZugeordnetePersonen { get; set; } = [];
    public List<EinheitZeile>               Einheiten           { get; set; } = [];
    public List<LehrgangAenderungspostenZeile> Aenderungsposten { get; set; } = [];
    public List<LehrgangAnhangZeile>        Anhaenge            { get; set; } = [];
    public LehrgangStatistik                Statistik           { get; set; } = new();
    public PersonenAuswahlListe             PersonenAuswahl     { get; set; } = new();

    // ── Meisterkurs-Daten (nur wenn LehrgangTyp == 0) ──────────────────────
    public List<MeisterAbschnittZeile>      MeisterAbschnitte       { get; set; } = [];
    public List<MeisterFachZeile>           MeisterFaecher          { get; set; } = [];
    public List<MeisterFunktionZeile>       MeisterFunktionen       { get; set; } = [];
    public List<MeisterPatientenUebersicht> MeisterPVUebersicht     { get; set; } = [];
    // Noten-Matrix: personId → fachId → Note
    public Dictionary<int, MeisterNotenPersonZeile> MeisterNotenMatrix { get; set; } = [];
    public List<MeisterTeilnehmerAuswahl>   MeisterTeilnehmerListe  { get; set; } = [];

    // ── GET ───────────────────────────────────────────────────────────────────

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle == 0)
            return RedirectToPage("/Zugriff/KeinZugriff");

        await LadeHilfslisten();

        if (id == null || id == 0)
        {
            Form = new LehrgangFormular { StartDatum = DateOnly.FromDateTime(DateTime.Today) };
            return Page();
        }

        var l = await _db.Lehrgaenge.FirstOrDefaultAsync(x => x.LehrgangId == id);
        if (l == null) return NotFound();

        Form = MapToForm(l);

        await LadePersonen(l.LehrgangId);
        await LadePersonenAuswahl();
        await LadeEinheiten(l.LehrgangId);
        await LadeAenderungsposten(l.LehrgangId);
        await LadeAnhaenge(l.LehrgangId);
        await LadeStatistik(l.LehrgangId, l.MaxTeilnehmer);

        if (l.LehrgangTyp == LehrgangTyp.Meistervorbereitung)
            await LadeMeisterkursDaten(l.LehrgangId);

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
            await LadeHilfslisten();
            if (Form.LehrgangId != 0)
            {
                await LadePersonen(Form.LehrgangId);
                await LadePersonenAuswahl();
                await LadeEinheiten(Form.LehrgangId);
                await LadeAenderungsposten(Form.LehrgangId);
                await LadeAnhaenge(Form.LehrgangId);
                await LadeStatistik(Form.LehrgangId, Form.MaxTeilnehmer);
            }
            return Page();
        }

        var user = User.Identity?.Name ?? "System";

        if (Form.LehrgangId == 0)
        {
            var nr = await NextNoAsync("LEHRGANG");
            var lehrgang = new Lehrgang
            {
                LehrgangNr      = nr,
                LehrgangTyp     = (LehrgangTyp)Form.LehrgangTyp,
                Bezeichnung     = Form.Bezeichnung,
                BezeichnungLang = Form.BezeichnungLang,
                Beschreibung    = Form.Beschreibung,
                ArtId           = Form.ArtId,
                StartDatum      = Form.StartDatum,
                EndDatum        = Form.EndDatum,
                MinTeilnehmer   = Form.MinTeilnehmer,
                MaxTeilnehmer   = Form.MaxTeilnehmer,
                Gebuehren       = Form.Gebuehren,
                Status          = LehrgangStatus.Planung,
                Notiz           = Form.Notiz,
                CreatedBy       = user,
                KostenLehrgang       = Form.KostenLehrgang,
                KostenInternatDZ     = Form.KostenInternatDZ,
                KostenInternatEZ     = Form.KostenInternatEZ,
                GrundzahlungBetrag   = Form.GrundzahlungBetrag,
                GrundzahlungTermin   = Form.GrundzahlungTermin,
                BeginnAbbuchung      = Form.BeginnAbbuchung,
                KautionWerkstatt     = Form.KautionWerkstatt,
                KautionInternat      = Form.KautionInternat,
                Verwaltungspauschale = Form.Verwaltungspauschale,
                AnzahlRaten          = Form.AnzahlRaten
            };
            _db.Lehrgaenge.Add(lehrgang);
            await _db.SaveChangesAsync();

            var belegNr = await NextNoAsync("AENDERUNG");
            AenderungspostenSchreiben(belegNr, lehrgang.LehrgangId, lehrgang.LehrgangNr, lehrgang.Bezeichnung,
                "LehrgangAngelegt", "Lehrgang", null, null, lehrgang.LehrgangNr, user);
            await _db.SaveChangesAsync();

            // Meisterkurs: Abschnitte, Fächer und leere Notenzeilen automatisch anlegen
            if (lehrgang.LehrgangTyp == LehrgangTyp.Meistervorbereitung)
                await _mk.AbschnittInitialisierenAsync(lehrgang.LehrgangId, user);

            return RedirectToPage(new { id = lehrgang.LehrgangId });
        }
        else
        {
            var l = await _db.Lehrgaenge.AsNoTracking()
                .FirstOrDefaultAsync(x => x.LehrgangId == Form.LehrgangId);
            if (l == null) return NotFound();

            var tracked = await _db.Lehrgaenge.FindAsync(Form.LehrgangId);
            if (tracked == null) return NotFound();

            var belegNr = await NextNoAsync("AENDERUNG");

            void Check(string feld, string? alt, string? neu)
            {
                if (string.Equals(alt?.Trim(), neu?.Trim(), StringComparison.OrdinalIgnoreCase)) return;
                AenderungspostenSchreiben(belegNr, l.LehrgangId, l.LehrgangNr, l.Bezeichnung,
                    "StammdatenGeaendert", "Lehrgang", feld, alt, neu, user);
            }
            void CheckInt(string feld, int alt, int neu)
            {
                if (alt == neu) return;
                AenderungspostenSchreiben(belegNr, l.LehrgangId, l.LehrgangNr, l.Bezeichnung,
                    "StammdatenGeaendert", "Lehrgang", feld, alt.ToString(), neu.ToString(), user);
            }
            void CheckDecimal(string feld, decimal? alt, decimal? neu)
            {
                if (alt == neu) return;
                var df = new CultureInfo("de-DE");
                AenderungspostenSchreiben(belegNr, l.LehrgangId, l.LehrgangNr, l.Bezeichnung,
                    "StammdatenGeaendert", "Lehrgang", feld,
                    alt.HasValue ? alt.Value.ToString("N2", df) + " €" : null,
                    neu.HasValue ? neu.Value.ToString("N2", df) + " €" : null, user);
            }
            void CheckDate(string feld, DateOnly? alt, DateOnly? neu)
            {
                if (alt == neu) return;
                AenderungspostenSchreiben(belegNr, l.LehrgangId, l.LehrgangNr, l.Bezeichnung,
                    "StammdatenGeaendert", "Lehrgang", feld,
                    alt?.ToString("dd.MM.yyyy"), neu?.ToString("dd.MM.yyyy"), user);
            }

            Check("Bezeichnung",          l.Bezeichnung,     Form.Bezeichnung);
            Check("Bezeichnung (lang)",   l.BezeichnungLang, Form.BezeichnungLang);
            Check("Beschreibung",         l.Beschreibung,    Form.Beschreibung);
            Check("Notiz",                l.Notiz,           Form.Notiz);
            CheckDate("Startdatum",       l.StartDatum,      Form.StartDatum);
            CheckDate("Enddatum",         l.EndDatum,        Form.EndDatum);
            CheckInt("Min. Teilnehmer",   l.MinTeilnehmer,   Form.MinTeilnehmer);
            CheckInt("Max. Teilnehmer",   l.MaxTeilnehmer,   Form.MaxTeilnehmer);
            CheckDecimal("Gebühren",      l.Gebuehren,       Form.Gebuehren);

            if (l.ArtId != Form.ArtId)
            {
                var altArt = l.ArtId.HasValue
                    ? await _db.LehrgangArten.Where(a => a.ArtId == l.ArtId).Select(a => a.Bezeichnung).FirstOrDefaultAsync()
                    : null;
                var neuArt = Form.ArtId.HasValue
                    ? await _db.LehrgangArten.Where(a => a.ArtId == Form.ArtId).Select(a => a.Bezeichnung).FirstOrDefaultAsync()
                    : null;
                AenderungspostenSchreiben(belegNr, l.LehrgangId, l.LehrgangNr, l.Bezeichnung,
                    "StammdatenGeaendert", "Lehrgang", "Lehrgangsart", altArt, neuArt, user);
            }

            // LehrgangTyp ist nach erstem Speichern unveränderlich
            tracked.Bezeichnung     = Form.Bezeichnung;
            tracked.BezeichnungLang = Form.BezeichnungLang;
            tracked.Beschreibung    = Form.Beschreibung;
            tracked.ArtId           = Form.ArtId;
            tracked.StartDatum      = Form.StartDatum;
            tracked.EndDatum        = Form.EndDatum;
            tracked.MinTeilnehmer   = Form.MinTeilnehmer;
            tracked.MaxTeilnehmer   = Form.MaxTeilnehmer;
            tracked.Gebuehren       = Form.Gebuehren;
            tracked.Notiz           = Form.Notiz;
            tracked.KostenLehrgang       = Form.KostenLehrgang;
            tracked.KostenInternatDZ     = Form.KostenInternatDZ;
            tracked.KostenInternatEZ     = Form.KostenInternatEZ;
            tracked.GrundzahlungBetrag   = Form.GrundzahlungBetrag;
            tracked.GrundzahlungTermin   = Form.GrundzahlungTermin;
            tracked.BeginnAbbuchung      = Form.BeginnAbbuchung;
            tracked.KautionWerkstatt     = Form.KautionWerkstatt;
            tracked.KautionInternat      = Form.KautionInternat;
            tracked.Verwaltungspauschale = Form.Verwaltungspauschale;
            tracked.AnzahlRaten          = Form.AnzahlRaten;
            tracked.ModifiedBy      = user;

            await _db.SaveChangesAsync();
            return RedirectToPage(new { id = tracked.LehrgangId });
        }
    }

    // ── POST: Status ändern ───────────────────────────────────────────────────

    public async Task<IActionResult> OnPostStatusAsync(int id, byte neuerStatus)
    {
        var l = await _db.Lehrgaenge.FindAsync(id);
        if (l == null) return NotFound();

        var user = User.Identity?.Name ?? "System";
        var altStatus = StatusText((byte)l.Status);
        l.Status     = (LehrgangStatus)neuerStatus;
        l.ModifiedBy = user;

        var belegNr = await NextNoAsync("AENDERUNG");
        AenderungspostenSchreiben(belegNr, l.LehrgangId, l.LehrgangNr, l.Bezeichnung,
            "StatusGeaendert", "Lehrgang", "Status", altStatus, StatusText(neuerStatus), user);

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    // ── POST: Absagen ─────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAbsagenAsync(int id)
    {
        var l = await _db.Lehrgaenge.FindAsync(id);
        if (l == null) return NotFound();

        if (l.Status != LehrgangStatus.Planung && l.Status != LehrgangStatus.AnmeldungOffen)
        {
            ModelState.AddModelError("", "Nur Lehrgänge in Planung oder mit offener Anmeldung können abgesagt werden.");
            return await OnGetAsync(id);
        }

        var user = User.Identity?.Name ?? "System";
        var altStatus = StatusText((byte)l.Status);
        l.Status     = LehrgangStatus.Storniert;
        l.ModifiedBy = user;

        var belegNr = await NextNoAsync("AENDERUNG");
        AenderungspostenSchreiben(belegNr, l.LehrgangId, l.LehrgangNr, l.Bezeichnung,
            "StatusGeaendert", "Lehrgang", "Status", altStatus, "Storniert", user);

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    // ── POST: Löschen ─────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostLoeschenAsync(int id)
    {
        var l = await _db.Lehrgaenge.FindAsync(id);
        if (l == null) return NotFound();

        if (l.Status != LehrgangStatus.Planung)
        {
            ModelState.AddModelError("", "Nur Lehrgänge im Status 'Planung' können gelöscht werden.");
            return await OnGetAsync(id);
        }

        var hatPersonen = await _db.LehrgangPersonen.AnyAsync(lp => lp.LehrgangId == id);
        if (hatPersonen)
        {
            ModelState.AddModelError("", "Lehrgang kann nicht gelöscht werden, da noch Personen zugeordnet sind.");
            return await OnGetAsync(id);
        }

        _db.Lehrgaenge.Remove(l);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    // ── POST: Person hinzufügen ───────────────────────────────────────────────

    public async Task<IActionResult> OnPostPersonHinzufuegenAsync(int id, int personId, byte rolle)
    {
        var exists = await _db.LehrgangPersonen
            .AnyAsync(lp => lp.LehrgangId == id && lp.PersonId == personId && lp.Rolle == (LehrgangPersonRolle)rolle);

        if (!exists)
        {
            var user = User.Identity?.Name ?? "System";

            // Betrieb-Snapshot aus aktueller PersonRolle
            var betriebRolle = await _db.PersonRollen
                .Where(r => r.PersonId == personId && r.Status == 0 && r.BetriebId != null)
                .Include(r => r.Betrieb)
                .FirstOrDefaultAsync();

            var person = await _db.Personen.FindAsync(personId);
            var lehrgang = await _db.Lehrgaenge.FindAsync(id);

            _db.LehrgangPersonen.Add(new LehrgangPerson
            {
                LehrgangId      = id,
                PersonId        = personId,
                Rolle           = (LehrgangPersonRolle)rolle,
                Status          = 1,
                AnmeldungsDatum = DateOnly.FromDateTime(DateTime.Today),
                BetriebId       = betriebRolle?.BetriebId,
                BetriebName     = betriebRolle?.Betrieb?.Name
            });

            if (person != null && lehrgang != null)
            {
                var belegNr = await NextNoAsync("AENDERUNG");
                AenderungspostenSchreiben(belegNr, id, lehrgang.LehrgangNr, lehrgang.Bezeichnung,
                    "PersonHinzugefuegt", "LehrgangPerson", "Person",
                    null, $"{person.PersonNr} {person.Nachname}, {person.Vorname} ({RolleText((LehrgangPersonRolle)rolle)})", user);
            }

            await _db.SaveChangesAsync();

            // Meisterkurs: Notenzeilen für neuen Teilnehmer anlegen
            if ((LehrgangPersonRolle)rolle == LehrgangPersonRolle.Teilnehmer && lehrgang != null
                && lehrgang.LehrgangTyp == LehrgangTyp.Meistervorbereitung)
            {
                await _mk.NoteZeilenFuerNeuenTeilnehmerAsync(id, personId, user);
            }
        }
        return RedirectToPage(new { id });
    }

    // ── POST: Person entfernen ────────────────────────────────────────────────

    public async Task<IActionResult> OnPostPersonEntfernenAsync(int id, int personId, byte rolle)
    {
        var lp = await _db.LehrgangPersonen
            .FirstOrDefaultAsync(x => x.LehrgangId == id && x.PersonId == personId && x.Rolle == (LehrgangPersonRolle)rolle);

        if (lp != null)
        {
            var person = await _db.Personen.FindAsync(personId);
            var lehrgang = await _db.Lehrgaenge.FindAsync(id);

            _db.LehrgangPersonen.Remove(lp);

            if (person != null && lehrgang != null)
            {
                var user = User.Identity?.Name ?? "System";
                var belegNr = await NextNoAsync("AENDERUNG");
                AenderungspostenSchreiben(belegNr, id, lehrgang.LehrgangNr, lehrgang.Bezeichnung,
                    "PersonEntfernt", "LehrgangPerson", "Person",
                    $"{person.PersonNr} {person.Nachname}, {person.Vorname} ({RolleText((LehrgangPersonRolle)rolle)})", null, user);
            }

            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { id });
    }

    // ── POST: Einheit speichern (Add + Edit) ──────────────────────────────────

    public async Task<IActionResult> OnPostEinheitSpeichernAsync()
    {
        if (string.IsNullOrWhiteSpace(EinheitForm.Thema))
        {
            ModelState.AddModelError("EinheitForm.Thema", "Thema ist erforderlich.");
            return await OnGetAsync(EinheitForm.LehrgangId);
        }

        var user = User.Identity?.Name ?? "System";
        var lehrgang = await _db.Lehrgaenge.FindAsync(EinheitForm.LehrgangId);
        if (lehrgang == null) return NotFound();

        if (EinheitForm.EinheitId == 0)
        {
            var einheit = new LehrgangEinheit
            {
                LehrgangId      = EinheitForm.LehrgangId,
                Datum           = EinheitForm.Datum,
                UhrzeitVon      = EinheitForm.UhrzeitVon,
                UhrzeitBis      = EinheitForm.UhrzeitBis,
                Thema           = EinheitForm.Thema,
                Inhalt          = EinheitForm.Inhalt,
                DozentPersonId  = EinheitForm.DozentPersonId,
                RaumBezeichnung = EinheitForm.RaumBezeichnung,
                EinheitTyp      = EinheitForm.EinheitTyp,
                Reihenfolge     = EinheitForm.Reihenfolge,
                Notiz           = EinheitForm.Notiz
            };
            _db.LehrgangEinheiten.Add(einheit);

            var belegNr = await NextNoAsync("AENDERUNG");
            AenderungspostenSchreiben(belegNr, lehrgang.LehrgangId, lehrgang.LehrgangNr, lehrgang.Bezeichnung,
                "EinheitHinzugefuegt", "LehrgangEinheit", "Thema",
                null, EinheitForm.Thema + " (" + EinheitForm.Datum.ToString("dd.MM.yyyy") + ")", user);
        }
        else
        {
            var einheit = await _db.LehrgangEinheiten.FindAsync(EinheitForm.EinheitId);
            if (einheit == null) return NotFound();

            einheit.Datum           = EinheitForm.Datum;
            einheit.UhrzeitVon      = EinheitForm.UhrzeitVon;
            einheit.UhrzeitBis      = EinheitForm.UhrzeitBis;
            einheit.Thema           = EinheitForm.Thema;
            einheit.Inhalt          = EinheitForm.Inhalt;
            einheit.DozentPersonId  = EinheitForm.DozentPersonId;
            einheit.RaumBezeichnung = EinheitForm.RaumBezeichnung;
            einheit.EinheitTyp      = EinheitForm.EinheitTyp;
            einheit.Reihenfolge     = EinheitForm.Reihenfolge;
            einheit.Notiz           = EinheitForm.Notiz;

            var belegNr = await NextNoAsync("AENDERUNG");
            AenderungspostenSchreiben(belegNr, lehrgang.LehrgangId, lehrgang.LehrgangNr, lehrgang.Bezeichnung,
                "EinheitGeaendert", "LehrgangEinheit", "Thema",
                null, EinheitForm.Thema + " (" + EinheitForm.Datum.ToString("dd.MM.yyyy") + ")", user);
        }

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = EinheitForm.LehrgangId });
    }

    // ── POST: Einheit löschen ─────────────────────────────────────────────────

    public async Task<IActionResult> OnPostEinheitLoeschenAsync(int einheitId, int lehrgangId)
    {
        var einheit = await _db.LehrgangEinheiten.FindAsync(einheitId);
        if (einheit != null)
        {
            _db.LehrgangEinheiten.Remove(einheit);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { id = lehrgangId });
    }

    // ── POST: Anhang hochladen ────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAnhangHochladenAsync(
        int lehrgangId, string bezeichnung, IFormFile? datei)
    {
        if (datei == null || datei.Length == 0 || string.IsNullOrWhiteSpace(bezeichnung))
            return RedirectToPage(new { id = lehrgangId });

        if (datei.Length > 10 * 1024 * 1024)
        {
            ModelState.AddModelError("", "Datei darf max. 10 MB groß sein.");
            return RedirectToPage(new { id = lehrgangId });
        }

        using var ms = new MemoryStream();
        await datei.CopyToAsync(ms);

        _db.Anhaenge.Add(new Anhang
        {
            BezugTyp       = "Lehrgang",
            BezugId        = lehrgangId,
            Bezeichnung    = bezeichnung,
            DateiName      = datei.FileName,
            DateiTyp       = datei.ContentType,
            DateiGroesse   = (int)datei.Length,
            Inhalt         = ms.ToArray(),
            HochgeladenVon = User.Identity?.Name,
            HochgeladenAm  = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = lehrgangId });
    }

    // ── POST: Anhang löschen ──────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAnhangLoeschenAsync(int anhangId, int lehrgangId)
    {
        var a = await _db.Anhaenge.FindAsync(anhangId);
        if (a != null) { _db.Anhaenge.Remove(a); await _db.SaveChangesAsync(); }
        return RedirectToPage(new { id = lehrgangId });
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private async Task LadeHilfslisten()
    {
        LehrgangArten = await _db.LehrgangArten.OrderBy(a => a.Reihenfolge).ToListAsync();

        var rawDozenten = await _db.PersonRollen
            .Where(r => r.Status == 0 && r.RolleTyp == PersonRolleTyp.Dozent)
            .Select(r => new DozentenAuswahl(r.Person.PersonId, r.Person.PersonNr,
                r.Person.Nachname + ", " + r.Person.Vorname))
            .ToListAsync();
        DozentenListe = rawDozenten.DistinctBy(d => d.PersonId).OrderBy(d => d.AnzeigeName).ToList();
    }

    private async Task LadePersonen(int lehrgangId)
    {
        var raw = await _db.LehrgangPersonen
            .Where(lp => lp.LehrgangId == lehrgangId)
            .Include(lp => lp.Person)
            .OrderBy(lp => lp.Rolle).ThenBy(lp => lp.Person.Nachname)
            .ToListAsync();

        ZugeordnetePersonen = raw.Select(lp => new LehrgangPersonZeile(
            lp.PersonId, lp.Person.PersonNr, lp.Person.Nachname, lp.Person.Vorname,
            lp.Rolle, RolleText(lp.Rolle),
            lp.Status, lp.Status switch {
                0 => "Warteliste", 1 => "Angemeldet", 2 => "Abgemeldet", 3 => "Bestanden", _ => ""
            },
            lp.AnmeldungsDatum, lp.GeplanteStunden, lp.BetriebName
        )).ToList();
    }

    private async Task LadePersonenAuswahl()
    {
        var personen = await _db.PersonRollen
            .Where(r => r.Status == 0 &&
                (r.RolleTyp == PersonRolleTyp.Teilnehmer || r.RolleTyp == PersonRolleTyp.Dozent))
            .Include(r => r.Person)
            .Select(r => new {
                r.Person.PersonId, r.Person.PersonNr,
                Name = r.Person.Nachname + ", " + r.Person.Vorname,
                RolleTyp = (byte)r.RolleTyp
            })
            .OrderBy(x => x.Name)
            .ToListAsync();

        PersonenAuswahl = new PersonenAuswahlListe(
            personen.Select(p => new PersonAuswahlItem(p.PersonId, p.PersonNr,
                $"{p.Name} ({(p.RolleTyp == 0 ? "TN" : "DO")})", p.RolleTyp)).ToList());
    }

    private async Task LadeEinheiten(int lehrgangId)
    {
        var raw = await _db.LehrgangEinheiten
            .Where(e => e.LehrgangId == lehrgangId)
            .Include(e => e.Dozent)
            .OrderBy(e => e.Datum).ThenBy(e => e.Reihenfolge).ThenBy(e => e.UhrzeitVon)
            .ToListAsync();

        Einheiten = raw.Select(e => new EinheitZeile(
            e.EinheitId, e.LehrgangId, e.Datum, e.UhrzeitVon, e.UhrzeitBis,
            e.EinheitTyp, e.Thema, e.Inhalt,
            e.DozentPersonId,
            e.Dozent != null ? e.Dozent.Nachname + ", " + e.Dozent.Vorname : null,
            e.RaumBezeichnung, e.Reihenfolge, e.Notiz
        )).ToList();
    }

    private async Task LadeAenderungsposten(int lehrgangId)
    {
        var posten = await _db.LehrgangAenderungsposten
            .Where(p => p.LehrgangId == lehrgangId)
            .OrderByDescending(p => p.Zeitstempel)
            .ToListAsync();

        Aenderungsposten = posten.Select(p => new LehrgangAenderungspostenZeile(
            p.PostenId, p.BelegNr, p.Ereignis, p.Tabelle, p.Feld,
            p.AlterWert, p.NeuerWert, p.Zeitstempel, p.AusfuehrendUser
        )).ToList();
    }

    private async Task LadeAnhaenge(int lehrgangId)
    {
        var liste = await _db.Anhaenge
            .Where(a => a.BezugTyp == "Lehrgang" && a.BezugId == lehrgangId)
            .OrderByDescending(a => a.HochgeladenAm)
            .ToListAsync();

        Anhaenge = liste.Select(a => new LehrgangAnhangZeile(
            a.AnhangId, a.Bezeichnung, a.DateiName, a.DateiTyp,
            a.DateiGroesse, a.HochgeladenVon, a.HochgeladenAm
        )).ToList();
    }

    private async Task LadeStatistik(int lehrgangId, int maxTN)
    {
        var personen = await _db.LehrgangPersonen
            .Where(lp => lp.LehrgangId == lehrgangId)
            .ToListAsync();

        var einheiten = await _db.LehrgangEinheiten
            .Where(e => e.LehrgangId == lehrgangId)
            .ToListAsync();

        var heute = DateOnly.FromDateTime(DateTime.Today);
        var naechste = einheiten
            .Where(e => e.Datum >= heute)
            .OrderBy(e => e.Datum).ThenBy(e => e.Reihenfolge)
            .FirstOrDefault();

        double gesamtStunden = einheiten
            .Where(e => e.UhrzeitVon.HasValue && e.UhrzeitBis.HasValue)
            .Sum(e => (e.UhrzeitBis!.Value - e.UhrzeitVon!.Value).TotalHours);

        int angemeldete = personen.Count(p => p.Rolle == LehrgangPersonRolle.Teilnehmer && p.Status == 1);

        Statistik = new LehrgangStatistik
        {
            AnzahlTeilnehmer    = angemeldete,
            AnzahlWarteliste    = personen.Count(p => p.Rolle == LehrgangPersonRolle.Teilnehmer && p.Status == 0),
            AnzahlDozenten      = personen.Count(p => p.Rolle == LehrgangPersonRolle.Dozent),
            FreiePlaetze        = maxTN == 0 ? -1 : maxTN - angemeldete,
            NaechstesEinheitDatum = naechste?.Datum,
            NaechstesEinheitThema = naechste?.Thema,
            GesamtStunden       = gesamtStunden,
            AnzahlEinheiten     = einheiten.Count,
            AnzahlAnhaenge      = Anhaenge.Count
        };
    }

    private void AenderungspostenSchreiben(
        string belegNr, int lehrgangId, string lgNr, string lgBezeichnung,
        string ereignis, string? tabelle, string? feld,
        string? alterWert, string? neuerWert, string user)
    {
        _db.LehrgangAenderungsposten.Add(new LehrgangAenderungsposten
        {
            BelegNr             = belegNr,
            LehrgangId          = lehrgangId,
            LehrgangNr          = lgNr,
            LehrgangBezeichnung = lgBezeichnung,
            Ereignis            = ereignis,
            Tabelle             = tabelle,
            Feld                = feld,
            AlterWert           = alterWert,
            NeuerWert           = neuerWert,
            Zeitstempel         = DateTime.UtcNow,
            AusfuehrendUser     = user
        });
    }

    private static LehrgangFormular MapToForm(Lehrgang l) => new()
    {
        LehrgangId      = l.LehrgangId,
        LehrgangNr      = l.LehrgangNr,
        LehrgangTyp     = (byte)l.LehrgangTyp,
        Bezeichnung     = l.Bezeichnung,
        BezeichnungLang = l.BezeichnungLang,
        Beschreibung    = l.Beschreibung,
        ArtId           = l.ArtId,
        StartDatum      = l.StartDatum,
        EndDatum        = l.EndDatum,
        MinTeilnehmer   = l.MinTeilnehmer,
        MaxTeilnehmer   = l.MaxTeilnehmer,
        Gebuehren       = l.Gebuehren,
        Status          = (byte)l.Status,
        Notiz           = l.Notiz,
        KostenLehrgang       = l.KostenLehrgang,
        KostenInternatDZ     = l.KostenInternatDZ,
        KostenInternatEZ     = l.KostenInternatEZ,
        GrundzahlungBetrag   = l.GrundzahlungBetrag,
        GrundzahlungTermin   = l.GrundzahlungTermin,
        BeginnAbbuchung      = l.BeginnAbbuchung,
        KautionWerkstatt     = l.KautionWerkstatt,
        KautionInternat      = l.KautionInternat,
        Verwaltungspauschale = l.Verwaltungspauschale,
        AnzahlRaten          = l.AnzahlRaten
    };

    private async Task<string> NextNoAsync(string serieCode)
    {
        var zeile = await _db.NoSerieZeilen
            .Where(z => z.NoSerieCode == serieCode && z.Offen)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Keine offene Nummernserie für '{serieCode}'.");

        long naechste = zeile.LastNoUsed == null
            ? long.Parse(zeile.StartingNo[(zeile.Prefix?.Length ?? 0)..])
            : long.Parse(zeile.LastNoUsed[(zeile.Prefix?.Length ?? 0)..]) + zeile.IncrementBy;

        var nr = (zeile.Prefix ?? "") + naechste.ToString().PadLeft(zeile.NummerLaenge, '0');
        zeile.LastNoUsed   = nr;
        zeile.LastDateUsed = DateOnly.FromDateTime(DateTime.Today);
        return nr;
    }

    // ── POST: Meister-Abschnitt Bezeichnung/Status speichern ─────────────────

    public async Task<IActionResult> OnPostAbschnittSpeichernAsync(
        int abschnittId, string bezeichnung, byte status, int lehrgangId)
    {
        var a = await _db.MeisterAbschnitte.FindAsync(abschnittId);
        if (a != null)
        {
            a.Bezeichnung = bezeichnung.Trim();
            a.Status      = status;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { id = lehrgangId });
    }

    // ── POST: Meister-Fach speichern (add/edit) ───────────────────────────────

    public async Task<IActionResult> OnPostFachSpeichernAsync(
        int fachId, int lehrgangId, string bezeichnung, decimal gewichtung, int reihenfolge)
    {
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

    // ── POST: Meister-Fach löschen ────────────────────────────────────────────

    public async Task<IActionResult> OnPostFachLoeschenAsync(int fachId, int lehrgangId)
    {
        var fach = await _db.MeisterFaecher.FindAsync(fachId);
        if (fach != null) { _db.MeisterFaecher.Remove(fach); await _db.SaveChangesAsync(); }
        return RedirectToPage(new { id = lehrgangId });
    }

    // ── POST: Note setzen ─────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostNoteSetzenAsync(
        int noteId, byte note, int? dozentPersonId, int lehrgangId)
    {
        var user = User.Identity?.Name ?? "System";
        await _mk.NoteSetzenAsync(noteId, note, dozentPersonId, user);
        return RedirectToPage(new { id = lehrgangId });
    }

    // ── POST: Funktion ändern (alte schließen, neue anlegen) ──────────────────

    public async Task<IActionResult> OnPostFunktionAendernAsync(
        int lehrgangId, byte funktion, int personId)
    {
        var user = User.Identity?.Name ?? "System";
        var heute = DateOnly.FromDateTime(DateTime.Today);

        // Alten Eintrag schließen
        var alt = await _db.MeisterFunktionen
            .Where(f => f.LehrgangId == lehrgangId && f.Funktion == funktion && f.GueltigBis == null)
            .FirstOrDefaultAsync();
        if (alt != null) alt.GueltigBis = heute;

        // Neue Person suchen
        var person = await _db.Personen.FindAsync(personId);
        if (person != null)
        {
            _db.MeisterFunktionen.Add(new MeisterFunktion
            {
                LehrgangId = lehrgangId,
                PersonId   = personId,
                PersonNr   = person.PersonNr,
                PersonName = person.Nachname + ", " + person.Vorname,
                Funktion   = funktion,
                GueltigAb  = heute,
                CreatedBy  = user,
                CreatedAt  = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = lehrgangId });
    }

    // ── POST: Funktion entfernen (GueltigBis = heute) ─────────────────────────

    public async Task<IActionResult> OnPostFunktionEntfernenAsync(int funktionId, int lehrgangId)
    {
        var f = await _db.MeisterFunktionen.FindAsync(funktionId);
        if (f != null)
        {
            f.GueltigBis = DateOnly.FromDateTime(DateTime.Today);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { id = lehrgangId });
    }

    // ── GET: Noten-CSV Export ─────────────────────────────────────────────────

    public async Task<IActionResult> OnGetNotenCsvAsync(int id)
    {
        var faecher = await _db.MeisterFaecher
            .Where(f => f.LehrgangId == id).OrderBy(f => f.Reihenfolge).ToListAsync();
        var noten = await _db.MeisterNoten
            .Where(n => n.LehrgangId == id).ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.Append("PersonNr;PersonName");
        foreach (var f in faecher) sb.Append($";{f.Bezeichnung}");
        sb.Append(";Gesamtnote\n");

        var personen = noten.Select(n => new { n.PersonId, n.PersonNr, n.PersonName })
            .Distinct().OrderBy(p => p.PersonName);

        foreach (var p in personen)
        {
            sb.Append($"{p.PersonNr};{p.PersonName}");
            decimal sum = 0; decimal gewSum = 0; bool allBewertet = true;
            foreach (var f in faecher)
            {
                var n = noten.FirstOrDefault(x => x.FachId == f.FachId && x.PersonId == p.PersonId);
                if (n?.Note.HasValue == true) { sum += n.Note.Value * f.Gewichtung; gewSum += f.Gewichtung; sb.Append($";{n.Note}"); }
                else { sb.Append(";"); allBewertet = false; }
            }
            sb.Append(allBewertet && gewSum > 0 ? $";{Math.Round(sum / gewSum, 1)}\n" : ";\n");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"Noten_{id}.csv");
    }

    // ── Meisterkurs: Lade-Methoden ────────────────────────────────────────────

    private async Task LadeMeisterkursDaten(int lehrgangId)
    {
        // Abschnitte
        var abschnitte = await _db.MeisterAbschnitte
            .Where(a => a.LehrgangId == lehrgangId)
            .OrderBy(a => a.Reihenfolge)
            .ToListAsync();

        var pvIds = abschnitte.Where(a => a.AbschnittTyp >= 1).Select(a => a.AbschnittId).ToList();
        var zuordnungCounts = await _db.MeisterPatientenZuordnungen
            .Where(z => pvIds.Contains(z.AbschnittId))
            .GroupBy(z => new { z.AbschnittId, z.BuchungsStatus })
            .Select(g => new { g.Key.AbschnittId, g.Key.BuchungsStatus, Count = g.Count() })
            .ToListAsync();

        MeisterAbschnitte = abschnitte.Select(a => new MeisterAbschnittZeile(
            a.AbschnittId, a.Nummer, a.Bezeichnung, a.AbschnittTyp, a.Status,
            zuordnungCounts.Where(z => z.AbschnittId == a.AbschnittId).Sum(z => z.Count),
            zuordnungCounts.Where(z => z.AbschnittId == a.AbschnittId && z.BuchungsStatus == 1).Sum(z => z.Count),
            zuordnungCounts.Where(z => z.AbschnittId == a.AbschnittId && z.BuchungsStatus == 2).Sum(z => z.Count)
        )).ToList();

        // Fächer
        var faecher = await _db.MeisterFaecher
            .Where(f => f.LehrgangId == lehrgangId)
            .OrderBy(f => f.Reihenfolge)
            .ToListAsync();
        MeisterFaecher = faecher.Select(f => new MeisterFachZeile(
            f.FachId, f.Bezeichnung, f.Gewichtung, f.Reihenfolge)).ToList();

        // Noten-Matrix
        var noten = await _db.MeisterNoten
            .Where(n => n.LehrgangId == lehrgangId)
            .ToListAsync();

        var personenIds = noten.Select(n => n.PersonId).Distinct().OrderBy(x => x).ToList();
        var fachGewichtung = faecher.ToDictionary(f => f.FachId, f => f.Gewichtung);
        MeisterNotenMatrix = new Dictionary<int, MeisterNotenPersonZeile>();
        foreach (var pid in personenIds)
        {
            var pNoten = noten.Where(n => n.PersonId == pid).ToList();
            var erste  = pNoten.FirstOrDefault();
            var fachNoten = faecher.ToDictionary(
                f => f.FachId,
                f => pNoten.FirstOrDefault(n => n.FachId == f.FachId));

            decimal? gesamt = null;
            var bewertete = pNoten.Where(n => n.Note.HasValue).ToList();
            if (bewertete.Count == faecher.Count && faecher.Count > 0)
            {
                var sum = bewertete.Sum(n => (decimal)n.Note!.Value
                    * (fachGewichtung.TryGetValue(n.FachId, out var gw) ? gw : 1m));
                var gew = faecher.Sum(f => f.Gewichtung);
                if (gew > 0) gesamt = Math.Round(sum / gew, 1);
            }

            MeisterNotenMatrix[pid] = new MeisterNotenPersonZeile(
                pid, erste?.PersonNr ?? "", erste?.PersonName ?? "",
                fachNoten, gesamt);
        }

        // Funktionen – alle 10 Slots anzeigen
        var aktiveFunktionen = await _db.MeisterFunktionen
            .Where(f => f.LehrgangId == lehrgangId && f.GueltigBis == null)
            .ToListAsync();

        MeisterFunktionen = Enumerable.Range(0, 10).Select(i =>
        {
            var aktiv = aktiveFunktionen.FirstOrDefault(f => f.Funktion == i);
            return new MeisterFunktionZeile(
                (byte)i,
                MeisterkursService.FunktionName((byte)i),
                aktiv?.FunktionId,
                aktiv?.PersonId,
                aktiv?.PersonNr,
                aktiv?.PersonName,
                aktiv?.GueltigAb);
        }).ToList();

        // PV-Übersicht (Abschnitte Typ 1+2)
        MeisterPVUebersicht = MeisterAbschnitte
            .Where(a => a.AbschnittTyp >= 1)
            .Select(a => new MeisterPatientenUebersicht(
                a.AbschnittId, a.Nummer, a.Bezeichnung, a.AbschnittTyp,
                a.AnzahlZuordnungen, a.AnzahlBestaetigt, a.AnzahlGebucht))
            .ToList();

        // Meisterschüler-Auswahl (Teilnehmer dieses Lehrgangs)
        MeisterTeilnehmerListe = ZugeordnetePersonen
            .Where(p => p.Rolle == LehrgangPersonRolle.Teilnehmer)
            .Select(p => new MeisterTeilnehmerAuswahl(p.PersonId, p.PersonNr,
                p.Nachname + ", " + p.Vorname))
            .OrderBy(p => p.Name)
            .ToList();
    }

    private static string StatusText(byte s) => s switch {
        0 => "Planung", 1 => "Anmeldung offen", 2 => "Aktiv",
        3 => "Abgeschlossen", 4 => "Storniert", _ => ""
    };

    private static string RolleText(LehrgangPersonRolle r) => r switch {
        LehrgangPersonRolle.Teilnehmer => "Teilnehmer",
        LehrgangPersonRolle.Dozent     => "Dozent",
        LehrgangPersonRolle.Assistent  => "Assistent",
        LehrgangPersonRolle.Gast       => "Gast",
        _ => r.ToString()
    };
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record LehrgangPersonZeile(
    int PersonId, string PersonNr, string Nachname, string Vorname,
    LehrgangPersonRolle Rolle, string RolleText,
    byte Status, string StatusText,
    DateOnly AnmeldungsDatum, decimal? GeplanteStunden, string? BetriebName);

public record EinheitZeile(
    int EinheitId, int LehrgangId, DateOnly Datum, TimeOnly? UhrzeitVon, TimeOnly? UhrzeitBis,
    byte EinheitTyp, string Thema, string? Inhalt,
    int? DozentPersonId, string? DozentName, string? Raum, int Reihenfolge, string? Notiz);

public record LehrgangAenderungspostenZeile(
    int PostenId, string BelegNr, string Ereignis, string? Tabelle,
    string? Feld, string? AlterWert, string? NeuerWert,
    DateTime Zeitstempel, string User);

public record LehrgangAnhangZeile(
    int AnhangId, string Bezeichnung, string DateiName, string DateiTyp,
    int DateiGroesse, string? HochgeladenVon, DateTime HochgeladenAm);

public record DozentenAuswahl(int PersonId, string PersonNr, string AnzeigeName);

public record PersonAuswahlItem(int PersonId, string PersonNr, string Text, byte RolleTyp);

public class PersonenAuswahlListe
{
    public List<PersonAuswahlItem> Items { get; } = [];
    public PersonenAuswahlListe() { }
    public PersonenAuswahlListe(List<PersonAuswahlItem> items) => Items = items;
}

public class LehrgangStatistik
{
    public int      AnzahlTeilnehmer     { get; set; }
    public int      AnzahlWarteliste     { get; set; }
    public int      AnzahlDozenten       { get; set; }
    public int      FreiePlaetze         { get; set; }  // -1 = unbegrenzt
    public DateOnly? NaechstesEinheitDatum { get; set; }
    public string?  NaechstesEinheitThema { get; set; }
    public double   GesamtStunden        { get; set; }
    public int      AnzahlEinheiten      { get; set; }
    public int      AnzahlAnhaenge       { get; set; }
}

public class LehrgangFormular
{
    public int      LehrgangId      { get; set; }
    public string   LehrgangNr      { get; set; } = "";
    public byte     LehrgangTyp     { get; set; } = 0;
    [Required(ErrorMessage = "Bezeichnung ist erforderlich")]
    public string   Bezeichnung     { get; set; } = "";
    public string?  BezeichnungLang { get; set; }
    public string?  Beschreibung    { get; set; }
    public int?     ArtId           { get; set; }
    [Required(ErrorMessage = "Startdatum ist erforderlich")]
    public DateOnly StartDatum      { get; set; }
    public DateOnly? EndDatum       { get; set; }
    public int      MinTeilnehmer   { get; set; } = 0;
    public int      MaxTeilnehmer   { get; set; } = 0;
    public decimal? Gebuehren       { get; set; }
    public byte     Status          { get; set; } = 0;
    public string?  Notiz           { get; set; }

    // Meisterkurs: Kosten
    public decimal?  KostenLehrgang       { get; set; }
    public decimal?  KostenInternatDZ     { get; set; }
    public decimal?  KostenInternatEZ     { get; set; }
    public decimal?  GrundzahlungBetrag   { get; set; }
    public DateOnly? GrundzahlungTermin   { get; set; }
    public DateOnly? BeginnAbbuchung      { get; set; }
    public decimal?  KautionWerkstatt     { get; set; }
    public decimal?  KautionInternat      { get; set; }
    public decimal?  Verwaltungspauschale { get; set; }
    public int?      AnzahlRaten          { get; set; }
}

public class EinheitFormular
{
    public int       EinheitId       { get; set; }
    public int       LehrgangId      { get; set; }
    public DateOnly  Datum           { get; set; }
    public TimeOnly? UhrzeitVon      { get; set; }
    public TimeOnly? UhrzeitBis      { get; set; }
    public string    Thema           { get; set; } = "";
    public string?   Inhalt          { get; set; }
    public int?      DozentPersonId  { get; set; }
    public string?   RaumBezeichnung { get; set; }
    public byte      EinheitTyp      { get; set; } = 0;
    public int       Reihenfolge     { get; set; } = 0;
    public string?   Notiz           { get; set; }
}

// ── Meisterkurs DTOs ──────────────────────────────────────────────────────────

public record MeisterAbschnittZeile(
    int AbschnittId, int Nummer, string Bezeichnung, byte AbschnittTyp, byte Status,
    int AnzahlZuordnungen, int AnzahlBestaetigt, int AnzahlGebucht);

public record MeisterFachZeile(int FachId, string Bezeichnung, decimal Gewichtung, int Reihenfolge);

public record MeisterFunktionZeile(
    byte Funktion, string FunktionName,
    int? FunktionId, int? PersonId, string? PersonNr, string? PersonName, DateOnly? GueltigAb);

public record MeisterPatientenUebersicht(
    int AbschnittId, int Nummer, string Bezeichnung, byte AbschnittTyp,
    int Gesamt, int Bestaetigt, int Gebucht);

public record MeisterTeilnehmerAuswahl(int PersonId, string PersonNr, string Name);

public class MeisterNotenPersonZeile
{
    public int     PersonId   { get; }
    public string  PersonNr   { get; }
    public string  PersonName { get; }
    public Dictionary<int, MeisterNote?> FachNoten { get; }
    public decimal? Gesamtnote { get; }
    public MeisterNotenPersonZeile(int personId, string nr, string name,
        Dictionary<int, MeisterNote?> fachNoten, decimal? gesamtnote)
    {
        PersonId   = personId;
        PersonNr   = nr;
        PersonName = name;
        FachNoten  = fachNoten;
        Gesamtnote = gesamtnote;
    }
}
