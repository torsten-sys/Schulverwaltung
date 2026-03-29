using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Domain.Interfaces;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Personen;

public class KarteModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    private readonly IPersonRolleService      _rolleService;

    public KarteModel(SchulverwaltungDbContext db, IPersonRolleService rolleService)
    {
        _db           = db;
        _rolleService = rolleService;
    }

    [BindProperty] public PersonFormular        Form          { get; set; } = new();
    [BindProperty] public DozentProfilFormular  DozentProfil  { get; set; } = new();
    [BindProperty] public PatientProfilFormular PatientProfil { get; set; } = new();

    public bool   IstNeu => Form.PersonId == 0;
    public string Titel  => IstNeu ? "Neue Person" : $"{Form.Vorname} {Form.Nachname}";

    public List<RolleZeile>             Rollen           { get; set; } = new();
    public List<AenderungspostenZeile>  Aenderungsposten { get; set; } = new();
    public List<BetriebAuswahl>         BetriebListe     { get; set; } = new();
    public List<AnhangZeile>            Anhaenge         { get; set; } = new();
    public KontaktStatistik             Statistik        { get; set; } = new();

    public bool HatTeilnehmer      => Rollen.Any(r => r.RolleTyp == PersonRolleTyp.Teilnehmer      && r.Aktiv);
    public bool HatDozent          => Rollen.Any(r => r.RolleTyp == PersonRolleTyp.Dozent          && r.Aktiv);
    public bool HatPatient         => Rollen.Any(r => r.RolleTyp == PersonRolleTyp.Patient         && r.Aktiv);
    public bool HatAnsprechpartner => Rollen.Any(r => r.RolleTyp == PersonRolleTyp.Ansprechpartner && r.Aktiv);
    public bool HatPruefer         => Rollen.Any(r => r.RolleTyp == PersonRolleTyp.Pruefer         && r.Aktiv);
    public bool HatBetreuer        => Rollen.Any(r => r.RolleTyp == PersonRolleTyp.Betreuer        && r.Aktiv);

    // ── GET ───────────────────────────────────────────────────────────────────

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle == 0)
            return RedirectToPage("/Zugriff/KeinZugriff");

        await LadeBetriebe();

        if (id == null || id == 0)
            return Page();

        var p = await _db.Personen
            .Include(x => x.Rollen).ThenInclude(r => r.Betrieb)
            .FirstOrDefaultAsync(x => x.PersonId == id);

        if (p == null) return NotFound();

        Form = MapToForm(p);

        await LadeRollenUndPosten();
        await LadeAnhaenge(p.PersonId);
        await LadeStatistik(p.PersonId);

        if (HatDozent)
            await LadeDozentProfil(p.PersonId);

        if (HatPatient)
            await LadePatientProfil(p.PersonId);

        return Page();
    }

    // ── GET: Foto ausliefern ──────────────────────────────────────────────────

    public async Task<IActionResult> OnGetFotoAsync(int id)
    {
        var p = await _db.Personen
            .Where(x => x.PersonId == id)
            .Select(x => new { x.Foto, x.FotoTyp })
            .FirstOrDefaultAsync();

        if (p?.Foto == null) return NotFound();
        return File(p.Foto, p.FotoTyp ?? "image/jpeg");
    }

    // ── GET: Anhang herunterladen ─────────────────────────────────────────────

    public async Task<IActionResult> OnGetAnhangAsync(int anhangId)
    {
        var a = await _db.Anhaenge.FindAsync(anhangId);
        if (a == null) return NotFound();
        return File(a.Inhalt, a.DateiTyp, a.DateiName);
    }

    // ── POST: Stammdaten speichern ────────────────────────────────────────────

    public async Task<IActionResult> OnPostSpeichernAsync()
    {
        await LadeBetriebe();

        if (!ModelState.IsValid)
        {
            if (Form.PersonId > 0)
            {
                await LadeRollenUndPosten();
                await LadeAnhaenge(Form.PersonId);
                await LadeStatistik(Form.PersonId);
                if (HatDozent) await LadeDozentProfil(Form.PersonId);
            }
            return Page();
        }

        if (Form.PersonId == 0)
        {
            // Neue Person anlegen
            var nr = await NextNoAsync("PERSON");
            var p  = new Person
            {
                PersonNr      = nr,
                Anrede        = Form.Anrede,
                Titel         = Form.Titel,
                Vorname       = Form.Vorname,
                Nachname      = Form.Nachname,
                Namenszusatz  = Form.Namenszusatz,
                Geburtsname   = Form.Geburtsname,
                Geburtsort    = Form.Geburtsort,
                Nationalitaet = string.IsNullOrWhiteSpace(Form.Nationalitaet) ? "deutsch" : Form.Nationalitaet,
                Geburtsdatum  = Form.Geburtsdatum,
                Geschlecht    = Form.Geschlecht,
                Strasse       = Form.Strasse,
                PLZ           = Form.PLZ,
                Ort           = Form.Ort,
                Land          = string.IsNullOrWhiteSpace(Form.Land) ? "Deutschland" : Form.Land,
                Email         = Form.Email,
                Telefon       = Form.Telefon,
                Mobil         = Form.Mobil,
                Gesperrt      = Form.Gesperrt,
                Notiz         = Form.Notiz
            };
            _db.Personen.Add(p);
            await _db.SaveChangesAsync();
            return RedirectToPage(new { id = p.PersonId });
        }
        else
        {
            // Bestehende Person laden – Snapshot für Änderungsprotokoll
            var alt = await _db.Personen.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PersonId == Form.PersonId);
            if (alt == null) return NotFound();

            var p = await _db.Personen.FindAsync(Form.PersonId);
            if (p == null) return NotFound();

            // Werte übernehmen
            p.Anrede        = Form.Anrede;
            p.Titel         = Form.Titel;
            p.Vorname       = Form.Vorname;
            p.Nachname      = Form.Nachname;
            p.Namenszusatz  = Form.Namenszusatz;
            p.Geburtsname   = Form.Geburtsname;
            p.Geburtsort    = Form.Geburtsort;
            p.Nationalitaet = string.IsNullOrWhiteSpace(Form.Nationalitaet) ? "deutsch" : Form.Nationalitaet;
            p.Geburtsdatum  = Form.Geburtsdatum;
            p.Geschlecht    = Form.Geschlecht;
            p.Strasse       = Form.Strasse;
            p.PLZ           = Form.PLZ;
            p.Ort           = Form.Ort;
            p.Land          = string.IsNullOrWhiteSpace(Form.Land) ? "Deutschland" : Form.Land;
            p.Email         = Form.Email;
            p.Telefon       = Form.Telefon;
            p.Mobil         = Form.Mobil;
            p.Gesperrt      = Form.Gesperrt;
            p.Notiz         = Form.Notiz;

            // Änderungsprotokoll – Felder vergleichen
            var aenderungen = new List<(string Feld, string? AltVal, string? NeuVal, string Ereignis)>();

            void Check(string feld, string? altVal, string? neuVal, string ereignis = "StammdatenGeaendert")
            {
                if ((altVal ?? "") != (neuVal ?? ""))
                    aenderungen.Add((feld, altVal, neuVal, ereignis));
            }

            Check("Anrede",       alt.Anrede,       p.Anrede);
            Check("Titel",        alt.Titel,         p.Titel);
            Check("Vorname",      alt.Vorname,       p.Vorname);
            Check("Nachname",     alt.Nachname,      p.Nachname);
            Check("Namenszusatz", alt.Namenszusatz,  p.Namenszusatz);
            Check("Geburtsname",  alt.Geburtsname,   p.Geburtsname);
            Check("Geburtsort",   alt.Geburtsort,    p.Geburtsort);
            Check("Nationalitaet", alt.Nationalitaet, p.Nationalitaet);
            Check("Geburtsdatum",
                alt.Geburtsdatum?.ToString("dd.MM.yyyy"),
                p.Geburtsdatum?.ToString("dd.MM.yyyy"));
            Check("Geschlecht", GeschlechtText(alt.Geschlecht), GeschlechtText(p.Geschlecht));
            Check("Email",    alt.Email,   p.Email);
            Check("Telefon",  alt.Telefon, p.Telefon);
            Check("Mobil",    alt.Mobil,   p.Mobil);
            Check("Gesperrt",
                alt.Gesperrt ? "Ja" : "Nein",
                p.Gesperrt   ? "Ja" : "Nein",
                p.Gesperrt != alt.Gesperrt ? "Gesperrt" : "StammdatenGeaendert");

            if (aenderungen.Any())
            {
                var belegNr = await NextNoAsync("AENDERUNG");
                var now     = DateTime.UtcNow;
                foreach (var (feld, altVal, neuVal, ereignis) in aenderungen)
                {
                    _db.PersonAenderungsposten.Add(new PersonAenderungsposten
                    {
                        BelegNr         = belegNr,
                        PersonId        = p.PersonId,
                        PersonNr        = p.PersonNr,
                        PersonName      = alt.AnzeigeName,
                        Ereignis        = ereignis,
                        Tabelle         = "Person",
                        Feld            = feld,
                        AlterWert       = altVal,
                        NeuerWert       = neuVal,
                        Zeitstempel     = now,
                        AusfuehrendUser = User.Identity?.Name ?? "System"
                    });
                }
            }

            await _db.SaveChangesAsync();
            return RedirectToPage(new { id = p.PersonId });
        }
    }

    // ── POST: Foto hochladen ──────────────────────────────────────────────────

    public async Task<IActionResult> OnPostFotoHochladenAsync(int id, IFormFile foto)
    {
        if (foto == null || foto.Length == 0)
        {
            TempData["Fehler"] = "Bitte eine Datei auswählen.";
            return RedirectToPage(new { id });
        }

        var erlaubteTypen = new[] { "image/jpeg", "image/jpg", "image/png" };
        if (!erlaubteTypen.Contains(foto.ContentType.ToLowerInvariant()))
        {
            TempData["Fehler"] = "Nur JPG und PNG-Dateien sind erlaubt.";
            return RedirectToPage(new { id });
        }

        if (foto.Length > 2 * 1024 * 1024)
        {
            TempData["Fehler"] = "Das Foto darf maximal 2 MB groß sein.";
            return RedirectToPage(new { id });
        }

        var p = await _db.Personen.FindAsync(id);
        if (p == null) return NotFound();

        using var ms = new MemoryStream();
        await foto.CopyToAsync(ms);
        p.Foto    = ms.ToArray();
        p.FotoTyp = foto.ContentType;

        var belegNr = await NextNoAsync("AENDERUNG");
        _db.PersonAenderungsposten.Add(new PersonAenderungsposten
        {
            BelegNr         = belegNr,
            PersonId        = p.PersonId,
            PersonNr        = p.PersonNr,
            PersonName      = p.AnzeigeName,
            Ereignis        = "FotoGeaendert",
            Tabelle         = "Person",
            Feld            = "Foto",
            AlterWert       = "–",
            NeuerWert       = foto.FileName,
            Zeitstempel     = DateTime.UtcNow,
            AusfuehrendUser = User.Identity?.Name ?? "System"
        });

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    // ── POST: Anhang hochladen ────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAnhangHochladenAsync(int id, IFormFile datei, string bezeichnung)
    {
        if (datei == null || datei.Length == 0)
        {
            TempData["Fehler"] = "Bitte eine Datei auswählen.";
            return RedirectToPage(new { id });
        }

        if (datei.Length > 10 * 1024 * 1024)
        {
            TempData["Fehler"] = "Anhang darf maximal 10 MB groß sein.";
            return RedirectToPage(new { id });
        }

        using var ms = new MemoryStream();
        await datei.CopyToAsync(ms);

        _db.Anhaenge.Add(new Anhang
        {
            BezugTyp       = "Person",
            BezugId        = id,
            Bezeichnung    = string.IsNullOrWhiteSpace(bezeichnung) ? datei.FileName : bezeichnung,
            DateiName      = datei.FileName,
            DateiTyp       = datei.ContentType,
            DateiGroesse   = (int)datei.Length,
            Inhalt         = ms.ToArray(),
            HochgeladenVon = User.Identity?.Name ?? "System",
            HochgeladenAm  = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    // ── POST: Anhang löschen ──────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAnhangLoeschenAsync(int id, int anhangId)
    {
        var a = await _db.Anhaenge.FindAsync(anhangId);
        if (a != null)
        {
            _db.Anhaenge.Remove(a);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { id });
    }

    // ── POST: Rolle zuweisen ──────────────────────────────────────────────────

    public async Task<IActionResult> OnPostRolleZuweisenAsync(int id, byte rolleTyp)
    {
        try
        {
            await _rolleService.RolleZuweisenAsync(
                id, (PersonRolleTyp)rolleTyp, null, User.Identity?.Name ?? "System");

            if ((PersonRolleTyp)rolleTyp == PersonRolleTyp.Dozent)
            {
                var existing = await _db.PersonDozentProfile.FindAsync(id);
                if (existing == null)
                {
                    _db.PersonDozentProfile.Add(new PersonDozentProfil { PersonId = id, Intern = true });
                    await _db.SaveChangesAsync();
                }
            }

            if ((PersonRolleTyp)rolleTyp == PersonRolleTyp.Patient)
            {
                var existing = await _db.PersonPatientProfile.FindAsync(id);
                if (existing == null)
                {
                    _db.PersonPatientProfile.Add(new PersonPatientProfil { PersonId = id });
                    await _db.SaveChangesAsync();
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Fehler"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    // ── POST: Betrieb einer Rolle setzen ─────────────────────────────────────

    public async Task<IActionResult> OnPostRolleBetriebSetzenAsync(int rolleId, int? betriebId)
    {
        var rolle = await _db.PersonRollen.Include(r => r.Betrieb).FirstOrDefaultAsync(r => r.PersonRolleId == rolleId);
        if (rolle == null) return NotFound();

        Betrieb? betrieb = betriebId.HasValue && betriebId.Value > 0
            ? await _db.Betriebe.FindAsync(betriebId.Value)
            : null;

        rolle.BetriebId = betrieb?.BetriebId;

        var person = await _db.Personen.AsNoTracking()
            .Where(p => p.PersonId == rolle.PersonId)
            .Select(p => new { p.PersonNr, p.AnzeigeName })
            .FirstOrDefaultAsync();

        if (person != null)
        {
            var belegNr = await NextNoAsync("AENDERUNG");
            _db.PersonAenderungsposten.Add(new PersonAenderungsposten
            {
                BelegNr         = belegNr,
                PersonId        = rolle.PersonId,
                PersonNr        = person.PersonNr,
                PersonName      = person.AnzeigeName,
                Ereignis        = "BetriebGeaendert",
                Tabelle         = "PersonRolle",
                Feld            = "Betrieb",
                NeuerWert       = betrieb?.Name ?? "–",
                Zeitstempel     = DateTime.UtcNow,
                AusfuehrendUser = User.Identity?.Name ?? "System"
            });
        }

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = rolle.PersonId });
    }

    // ── POST: Rolle entfernen ─────────────────────────────────────────────────

    public async Task<IActionResult> OnPostRolleEntfernenAsync(int id, byte rolleTyp)
    {
        try
        {
            await _rolleService.RolleEntfernenAsync(
                id, (PersonRolleTyp)rolleTyp, User.Identity?.Name ?? "System");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Fehler"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    // ── POST: Dozent-Profil speichern ─────────────────────────────────────────

    public async Task<IActionResult> OnPostDozentProfilSpeichernAsync()
    {
        var alt = await _db.PersonDozentProfile.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PersonId == DozentProfil.PersonId);
        if (alt == null) return NotFound();

        var dp = await _db.PersonDozentProfile.FindAsync(DozentProfil.PersonId);
        if (dp == null) return NotFound();

        // Rohe IBAN speichern (Leerzeichen entfernen)
        var ibanRoh = DozentProfil.IBAN?.Replace(" ", "").ToUpperInvariant();

        dp.Kuerzel              = DozentProfil.Kuerzel;
        dp.Intern               = DozentProfil.Intern;
        dp.Bemerkungen          = DozentProfil.Bemerkungen;
        dp.MaxStundenProWoche   = DozentProfil.MaxStundenProWoche;
        dp.IstOrthopaedie       = DozentProfil.IstOrthopaedie;
        dp.IstPodologie         = DozentProfil.IstPodologie;
        dp.IstMedizin           = DozentProfil.IstMedizin;
        dp.KostenTheoriestunde  = DozentProfil.KostenTheoriestunde;
        dp.KostenPraxisstunde   = DozentProfil.KostenPraxisstunde;
        dp.Fahrtkosten          = DozentProfil.Fahrtkosten;
        dp.IBAN                 = ibanRoh;

        // Änderungsprotokoll DozentProfil
        var person = await _db.Personen.AsNoTracking()
            .Where(p => p.PersonId == dp.PersonId)
            .Select(p => new { p.PersonNr, p.AnzeigeName })
            .FirstOrDefaultAsync();

        var aenderungen = new List<(string Feld, string? AltVal, string? NeuVal)>();

        void CheckDp(string feld, string? altVal, string? neuVal)
        {
            if ((altVal ?? "") != (neuVal ?? ""))
                aenderungen.Add((feld, altVal, neuVal));
        }

        CheckDp("Kuerzel",            alt.Kuerzel,               dp.Kuerzel);
        CheckDp("Intern",             alt.Intern ? "Ja" : "Nein", dp.Intern ? "Ja" : "Nein");
        CheckDp("MaxStundenProWoche", alt.MaxStundenProWoche?.ToString("F2"), dp.MaxStundenProWoche?.ToString("F2"));
        CheckDp("Bemerkungen",        alt.Bemerkungen,            dp.Bemerkungen);
        CheckDp("IstOrthopaedie",     alt.IstOrthopaedie ? "Ja" : "Nein", dp.IstOrthopaedie ? "Ja" : "Nein");
        CheckDp("IstPodologie",       alt.IstPodologie   ? "Ja" : "Nein", dp.IstPodologie   ? "Ja" : "Nein");
        CheckDp("IstMedizin",         alt.IstMedizin     ? "Ja" : "Nein", dp.IstMedizin     ? "Ja" : "Nein");
        CheckDp("KostenTheoriestunde", alt.KostenTheoriestunde?.ToString("F2"), dp.KostenTheoriestunde?.ToString("F2"));
        CheckDp("KostenPraxisstunde",  alt.KostenPraxisstunde?.ToString("F2"),  dp.KostenPraxisstunde?.ToString("F2"));
        CheckDp("Fahrtkosten",         alt.Fahrtkosten?.ToString("F2"),          dp.Fahrtkosten?.ToString("F2"));
        // IBAN maskiert im Protokoll
        CheckDp("IBAN", MaskIban(alt.IBAN), MaskIban(dp.IBAN));

        if (aenderungen.Any() && person != null)
        {
            var belegNr = await NextNoAsync("AENDERUNG");
            var now     = DateTime.UtcNow;
            foreach (var (feld, altVal, neuVal) in aenderungen)
            {
                _db.PersonAenderungsposten.Add(new PersonAenderungsposten
                {
                    BelegNr         = belegNr,
                    PersonId        = dp.PersonId,
                    PersonNr        = person.PersonNr,
                    PersonName      = person.AnzeigeName,
                    Ereignis        = "DozentProfilGeaendert",
                    Tabelle         = "PersonDozentProfil",
                    Feld            = feld,
                    AlterWert       = altVal,
                    NeuerWert       = neuVal,
                    Zeitstempel     = now,
                    AusfuehrendUser = User.Identity?.Name ?? "System"
                });
            }
        }

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = dp.PersonId });
    }

    // ── POST: Patienten-Profil speichern ──────────────────────────────────────

    public async Task<IActionResult> OnPostPatientProfilSpeichernAsync()
    {
        var alt = await _db.PersonPatientProfile.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PersonId == PatientProfil.PersonId);
        if (alt == null) return NotFound();

        var pp = await _db.PersonPatientProfile.FindAsync(PatientProfil.PersonId);
        if (pp == null) return NotFound();

        pp.Groesse            = PatientProfil.Groesse;
        pp.Gewicht            = PatientProfil.Gewicht;
        pp.IstDiabetiker      = PatientProfil.IstDiabetiker;
        pp.GeeignetPV1        = PatientProfil.GeeignetPV1;
        pp.GeeignetPV2        = PatientProfil.GeeignetPV2;
        pp.GeeignetPV3        = PatientProfil.GeeignetPV3;
        pp.GeeignetPV4        = PatientProfil.GeeignetPV4;
        pp.GeeignetPVPruefung = PatientProfil.GeeignetPVPruefung;
        pp.Bemerkungen        = PatientProfil.Bemerkungen;

        // Änderungsprotokoll
        var person = await _db.Personen.AsNoTracking()
            .Where(p => p.PersonId == pp.PersonId)
            .Select(p => new { p.PersonNr, p.AnzeigeName })
            .FirstOrDefaultAsync();

        var aenderungen = new List<(string Feld, string? AltVal, string? NeuVal)>();

        void CheckPp(string feld, string? altVal, string? neuVal)
        {
            if ((altVal ?? "") != (neuVal ?? ""))
                aenderungen.Add((feld, altVal, neuVal));
        }

        CheckPp("Größe",                    alt.Groesse?.ToString(),                    pp.Groesse?.ToString());
        CheckPp("Gewicht",                   alt.Gewicht?.ToString("F1"),               pp.Gewicht?.ToString("F1"));
        CheckPp("Diabetiker",                alt.IstDiabetiker      ? "Ja" : "Nein",   pp.IstDiabetiker      ? "Ja" : "Nein");
        CheckPp("Patientenversorgung 1",     alt.GeeignetPV1        ? "Ja" : "Nein",   pp.GeeignetPV1        ? "Ja" : "Nein");
        CheckPp("Patientenversorgung 2",     alt.GeeignetPV2        ? "Ja" : "Nein",   pp.GeeignetPV2        ? "Ja" : "Nein");
        CheckPp("Patientenversorgung 3",     alt.GeeignetPV3        ? "Ja" : "Nein",   pp.GeeignetPV3        ? "Ja" : "Nein");
        CheckPp("Patientenversorgung 4",     alt.GeeignetPV4        ? "Ja" : "Nein",   pp.GeeignetPV4        ? "Ja" : "Nein");
        CheckPp("Patientenversorgung Prüfung", alt.GeeignetPVPruefung ? "Ja" : "Nein", pp.GeeignetPVPruefung ? "Ja" : "Nein");
        CheckPp("Bemerkungen",               alt.Bemerkungen,                           pp.Bemerkungen);

        if (aenderungen.Any() && person != null)
        {
            var belegNr = await NextNoAsync("AENDERUNG");
            var now     = DateTime.UtcNow;
            foreach (var (feld, altVal, neuVal) in aenderungen)
            {
                _db.PersonAenderungsposten.Add(new PersonAenderungsposten
                {
                    BelegNr         = belegNr,
                    PersonId        = pp.PersonId,
                    PersonNr        = person.PersonNr,
                    PersonName      = person.AnzeigeName,
                    Ereignis        = "PatientProfilGeaendert",
                    Tabelle         = "PersonPatientProfil",
                    Feld            = feld,
                    AlterWert       = altVal,
                    NeuerWert       = neuVal,
                    Zeitstempel     = now,
                    AusfuehrendUser = User.Identity?.Name ?? "System"
                });
            }
        }

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = pp.PersonId });
    }

    // ── POST: Person löschen ──────────────────────────────────────────────────

    public async Task<IActionResult> OnPostLoeschenAsync(int id)
    {
        var hatAktiv = await _db.PersonRollen.AnyAsync(r => r.PersonId == id && r.Status == 0);
        if (hatAktiv)
        {
            ModelState.AddModelError("", "Person kann nicht gelöscht werden, solange aktive Rollen vorhanden sind.");
            return await OnGetAsync(id);
        }

        var p = await _db.Personen.FindAsync(id);
        if (p == null) return NotFound();

        _db.Personen.Remove(p);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private async Task LadeBetriebe()
    {
        BetriebListe = await _db.Betriebe
            .OrderBy(b => b.Name)
            .Select(b => new BetriebAuswahl(b.BetriebId, b.Name))
            .ToListAsync();
    }

    private async Task LadeRollenUndPosten()
    {
        if (Form.PersonId == 0) return;

        Rollen = await _db.PersonRollen
            .Where(r => r.PersonId == Form.PersonId)
            .Include(r => r.Betrieb)
            .OrderBy(r => r.Status).ThenBy(r => r.RolleTyp)
            .Select(r => new RolleZeile(
                r.PersonRolleId, r.RolleTyp, r.Status == 0,
                r.Betrieb != null ? r.Betrieb.Name : null,
                r.GueltigAb, r.GueltigBis, r.Notiz))
            .ToListAsync();

        Aenderungsposten = await _db.PersonAenderungsposten
            .Where(a => a.PersonId == Form.PersonId)
            .OrderByDescending(a => a.Zeitstempel)
            .Select(a => new AenderungspostenZeile(
                a.Zeitstempel, a.Ereignis, a.Feld, a.AlterWert, a.NeuerWert, a.AusfuehrendUser))
            .ToListAsync();
    }

    private async Task LadeAnhaenge(int personId)
    {
        Anhaenge = await _db.Anhaenge
            .Where(a => a.BezugTyp == "Person" && a.BezugId == personId)
            .OrderByDescending(a => a.HochgeladenAm)
            .Select(a => new AnhangZeile(
                a.AnhangId, a.Bezeichnung, a.DateiName, a.DateiTyp,
                a.DateiGroesse, a.HochgeladenAm, a.HochgeladenVon))
            .ToListAsync();
    }

    private async Task LadeStatistik(int personId)
    {
        Statistik = new KontaktStatistik
        {
            AnzahlRollenAktiv = await _db.PersonRollen
                .CountAsync(r => r.PersonId == personId && r.Status == 0),
            AnzahlLehrgaengeGesamt = await _db.LehrgangPersonen
                .CountAsync(lp => lp.PersonId == personId),
            LaufendeLehrgaenge = await _db.LehrgangPersonen
                .CountAsync(lp => lp.PersonId == personId &&
                                  lp.Lehrgang.Status == LehrgangStatus.Aktiv),
            AbgeschlosseneLehrgaenge = await _db.LehrgangPersonen
                .CountAsync(lp => lp.PersonId == personId &&
                                  lp.Lehrgang.Status == LehrgangStatus.Abgeschlossen),
            AbgebrocheneLehrgaenge = await _db.LehrgangPersonen
                .CountAsync(lp => lp.PersonId == personId &&
                                  lp.Lehrgang.Status == LehrgangStatus.Storniert)
        };
    }

    private async Task LadeDozentProfil(int personId)
    {
        var dp = await _db.PersonDozentProfile.FindAsync(personId);
        if (dp != null)
            DozentProfil = new DozentProfilFormular
            {
                PersonId             = dp.PersonId,
                Kuerzel              = dp.Kuerzel,
                Intern               = dp.Intern,
                Bemerkungen          = dp.Bemerkungen,
                MaxStundenProWoche   = dp.MaxStundenProWoche,
                IstOrthopaedie       = dp.IstOrthopaedie,
                IstPodologie         = dp.IstPodologie,
                IstMedizin           = dp.IstMedizin,
                KostenTheoriestunde  = dp.KostenTheoriestunde,
                KostenPraxisstunde   = dp.KostenPraxisstunde,
                Fahrtkosten          = dp.Fahrtkosten,
                IBAN                 = dp.IBAN
            };
        else
            DozentProfil = new DozentProfilFormular { PersonId = personId };
    }

    private async Task LadePatientProfil(int personId)
    {
        var pp = await _db.PersonPatientProfile.FindAsync(personId);
        if (pp != null)
            PatientProfil = new PatientProfilFormular
            {
                PersonId           = pp.PersonId,
                Groesse            = pp.Groesse,
                Gewicht            = pp.Gewicht,
                IstDiabetiker      = pp.IstDiabetiker,
                GeeignetPV1        = pp.GeeignetPV1,
                GeeignetPV2        = pp.GeeignetPV2,
                GeeignetPV3        = pp.GeeignetPV3,
                GeeignetPV4        = pp.GeeignetPV4,
                GeeignetPVPruefung = pp.GeeignetPVPruefung,
                Bemerkungen        = pp.Bemerkungen
            };
        else
            PatientProfil = new PatientProfilFormular { PersonId = personId };
    }

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

    private static string? MaskIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return null;
        var raw = iban.Replace(" ", "");
        if (raw.Length < 4) return new string('*', raw.Length);
        var masked = raw[..2] + new string('*', raw.Length - 4) + raw[^2..];
        // Gruppen à 4 Zeichen mit Leerzeichen
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < masked.Length; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append(' ');
            sb.Append(masked[i]);
        }
        return sb.ToString();
    }

    private static string? GeschlechtText(byte? geschlecht) => geschlecht switch
    {
        1 => "Männlich",
        2 => "Weiblich",
        3 => "Divers",
        _ => null
    };

    private static PersonFormular MapToForm(Person p) => new()
    {
        PersonId      = p.PersonId,
        PersonNr      = p.PersonNr,
        Anrede        = p.Anrede,
        Titel         = p.Titel,
        Vorname       = p.Vorname,
        Nachname      = p.Nachname,
        Namenszusatz  = p.Namenszusatz,
        Geburtsname   = p.Geburtsname,
        Geburtsort    = p.Geburtsort,
        Nationalitaet = p.Nationalitaet ?? "deutsch",
        Geburtsdatum  = p.Geburtsdatum,
        Geschlecht    = p.Geschlecht,
        Strasse       = p.Strasse,
        PLZ           = p.PLZ,
        Ort           = p.Ort,
        Land          = p.Land,
        Email         = p.Email,
        Telefon       = p.Telefon,
        Mobil         = p.Mobil,
        Gesperrt      = p.Gesperrt,
        Notiz         = p.Notiz,
        HatFoto       = p.Foto != null
    };
}

// ── DTOs / View-Records ───────────────────────────────────────────────────────

public record RolleZeile(
    int PersonRolleId, PersonRolleTyp RolleTyp, bool Aktiv,
    string? BetriebName, DateOnly GueltigAb, DateOnly? GueltigBis, string? Notiz);

public record AenderungspostenZeile(
    DateTime Zeitstempel, string Ereignis, string? Feld,
    string? AlterWert, string? NeuerWert, string AusfuehrendUser);

public record BetriebAuswahl(int BetriebId, string Name);

public record AnhangZeile(
    int AnhangId, string Bezeichnung, string DateiName, string DateiTyp,
    int DateiGroesse, DateTime HochgeladenAm, string? HochgeladenVon);

public class KontaktStatistik
{
    public int AnzahlRollenAktiv         { get; set; }
    public int AnzahlLehrgaengeGesamt    { get; set; }
    public int LaufendeLehrgaenge        { get; set; }
    public int AbgeschlosseneLehrgaenge  { get; set; }
    public int AbgebrocheneLehrgaenge    { get; set; }
}

public class PersonFormular
{
    public int       PersonId      { get; set; }
    public string    PersonNr      { get; set; } = "";
    public string?   Anrede        { get; set; }
    public string?   Titel         { get; set; }

    [Required(ErrorMessage = "Vorname ist erforderlich")]
    public string    Vorname       { get; set; } = "";

    [Required(ErrorMessage = "Nachname ist erforderlich")]
    public string    Nachname      { get; set; } = "";

    public string?   Namenszusatz  { get; set; }
    public string?   Geburtsname   { get; set; }
    public string?   Geburtsort    { get; set; }
    public string    Nationalitaet { get; set; } = "deutsch";
    public DateOnly? Geburtsdatum  { get; set; }
    public byte?     Geschlecht    { get; set; }
    public string?   Strasse       { get; set; }
    public string?   PLZ           { get; set; }
    public string?   Ort           { get; set; }
    public string    Land          { get; set; } = "Deutschland";
    public string?   Email         { get; set; }
    public string?   Telefon       { get; set; }
    public string?   Mobil         { get; set; }
    public bool      Gesperrt      { get; set; }
    public string?   Notiz         { get; set; }
    public bool      HatFoto       { get; set; }
}

public class DozentProfilFormular
{
    public int      PersonId            { get; set; }
    public string?  Kuerzel             { get; set; }
    public bool     Intern              { get; set; } = true;
    public decimal? MaxStundenProWoche  { get; set; }
    public string?  Bemerkungen         { get; set; }

    // Fachbereiche
    public bool     IstOrthopaedie      { get; set; } = false;
    public bool     IstPodologie        { get; set; } = false;
    public bool     IstMedizin          { get; set; } = false;

    // Vergütung
    public decimal? KostenTheoriestunde { get; set; }
    public decimal? KostenPraxisstunde  { get; set; }
    public decimal? Fahrtkosten         { get; set; }
    public string?  IBAN                { get; set; }
}

public class PatientProfilFormular
{
    public int      PersonId           { get; set; }
    public int?     Groesse            { get; set; }
    public decimal? Gewicht            { get; set; }
    public bool     IstDiabetiker      { get; set; } = false;
    public bool     GeeignetPV1        { get; set; } = false;
    public bool     GeeignetPV2        { get; set; } = false;
    public bool     GeeignetPV3        { get; set; } = false;
    public bool     GeeignetPV4        { get; set; } = false;
    public bool     GeeignetPVPruefung { get; set; } = false;
    public string?  Bemerkungen        { get; set; }
}
