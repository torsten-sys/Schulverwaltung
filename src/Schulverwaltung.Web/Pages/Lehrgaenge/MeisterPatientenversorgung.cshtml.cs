using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class MeisterPatientenversorgungModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    private readonly MeisterkursService       _mk;
    public MeisterPatientenversorgungModel(SchulverwaltungDbContext db, MeisterkursService mk)
    {
        _db = db; _mk = mk;
    }

    [BindProperty(SupportsGet = true)] public int LehrgangId  { get; set; }
    [BindProperty(SupportsGet = true)] public int AbschnittId { get; set; }

    public string       LehrgangNr        { get; set; } = "";
    public string       LehrgangBez       { get; set; } = "";
    public MeisterAbschnitt? Abschnitt    { get; set; }
    public List<ZuordnungZeile>   Zuordnungen        { get; set; } = [];
    public List<PatientAuswahl>   PatientenListe      { get; set; } = [];
    public List<TeilnehmerAusw>   TeilnehmerListe     { get; set; } = [];
    public string? Fehler { get; set; }

    // ── GET ──────────────────────────────────────────────────────────────────

    public async Task<IActionResult> OnGetAsync()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 2)
            return RedirectToPage("/Zugriff/KeinZugriff");
        if (benutzer.AppRolle == 2)
        {
            var darfZugreifen = await _db.LehrgangPersonen
                .AnyAsync(lp => lp.LehrgangId == LehrgangId
                             && lp.PersonId == benutzer.PersonId
                             && lp.Rolle == LehrgangPersonRolle.Dozent);
            if (!darfZugreifen)
                return RedirectToPage("/Zugriff/KeinZugriff");
        }

        var lehrgang = await _db.Lehrgaenge.FindAsync(LehrgangId);
        if (lehrgang == null) return NotFound();
        LehrgangNr  = lehrgang.LehrgangNr;
        LehrgangBez = lehrgang.Bezeichnung;

        Abschnitt = await _db.MeisterAbschnitte.FindAsync(AbschnittId);
        if (Abschnitt == null) return NotFound();

        await LadeDaten();
        return Page();
    }

    // ── POST: Zuordnung anlegen/bearbeiten ────────────────────────────────────

    public async Task<IActionResult> OnPostZuordnungSpeichernAsync(
        [FromForm] ZuordnungFormular form)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 2)
            return RedirectToPage("/Zugriff/KeinZugriff");
        if (benutzer.AppRolle == 2)
        {
            var darfZugreifen = await _db.LehrgangPersonen
                .AnyAsync(lp => lp.LehrgangId == LehrgangId
                             && lp.PersonId == benutzer.PersonId
                             && lp.Rolle == LehrgangPersonRolle.Dozent);
            if (!darfZugreifen)
                return RedirectToPage("/Zugriff/KeinZugriff");
        }

        var user = User.Identity?.Name ?? "System";

        if (form.ZuordnungId == 0)
        {
            // Patient-Snapshot
            var patient = await _db.Personen.FindAsync(form.PatientPersonId);
            // MS1-Snapshot
            var ms1 = await _db.Personen.FindAsync(form.Meisterschueler1PersonId);
            // MS2-Snapshot (optional)
            Schulverwaltung.Domain.Entities.Person? ms2 = null;
            if (form.Meisterschueler2PersonId.HasValue)
                ms2 = await _db.Personen.FindAsync(form.Meisterschueler2PersonId.Value);

            if (patient == null || ms1 == null)
            {
                await LadeDaten();
                Fehler = "Patient und Meisterschüler 1 sind Pflichtfelder.";
                return Page();
            }

            var z = new MeisterPatientenZuordnung
            {
                LehrgangId               = LehrgangId,
                AbschnittId              = AbschnittId,
                PatientPersonId          = patient.PersonId,
                PatientPersonNr          = patient.PersonNr,
                PatientName              = patient.Nachname + ", " + patient.Vorname,
                Meisterschueler1PersonId = ms1.PersonId,
                Meisterschueler1Nr       = ms1.PersonNr,
                Meisterschueler1Name     = ms1.Nachname + ", " + ms1.Vorname,
                Meisterschueler2PersonId = ms2?.PersonId,
                Meisterschueler2Nr       = ms2?.PersonNr,
                Meisterschueler2Name     = ms2 != null ? ms2.Nachname + ", " + ms2.Vorname : null,
                IstErsatzpatient         = form.IstErsatzpatient,
                ZuordnungsStatus         = form.ZuordnungsStatus,
                Notiz                    = form.Notiz,
                CreatedBy                = user
            };
            _db.MeisterPatientenZuordnungen.Add(z);
            await _db.SaveChangesAsync();

            // Termine anlegen
            await SpeichereTermine(z.ZuordnungId, form);
        }
        else
        {
            var z = await _db.MeisterPatientenZuordnungen
                .Include(x => x.Termine)
                .FirstOrDefaultAsync(x => x.ZuordnungId == form.ZuordnungId);
            if (z == null) return NotFound();

            if (z.BuchungsStatus == 0) // Nur bei Planung alles änderbar
            {
                var patient = await _db.Personen.FindAsync(form.PatientPersonId);
                if (patient != null)
                {
                    z.PatientPersonId = patient.PersonId;
                    z.PatientPersonNr = patient.PersonNr;
                    z.PatientName     = patient.Nachname + ", " + patient.Vorname;
                }
                var ms1 = await _db.Personen.FindAsync(form.Meisterschueler1PersonId);
                if (ms1 != null)
                {
                    z.Meisterschueler1PersonId = ms1.PersonId;
                    z.Meisterschueler1Nr       = ms1.PersonNr;
                    z.Meisterschueler1Name     = ms1.Nachname + ", " + ms1.Vorname;
                }
                Schulverwaltung.Domain.Entities.Person? ms2 = null;
                if (form.Meisterschueler2PersonId.HasValue)
                    ms2 = await _db.Personen.FindAsync(form.Meisterschueler2PersonId.Value);
                z.Meisterschueler2PersonId = ms2?.PersonId;
                z.Meisterschueler2Nr       = ms2?.PersonNr;
                z.Meisterschueler2Name     = ms2 != null ? ms2.Nachname + ", " + ms2.Vorname : null;
                z.IstErsatzpatient         = form.IstErsatzpatient;
            }
            z.ZuordnungsStatus = form.ZuordnungsStatus;
            z.Notiz            = form.Notiz;

            await _db.SaveChangesAsync();
            await SpeichereTermine(z.ZuordnungId, form);
        }

        return RedirectToPage(new { LehrgangId, AbschnittId });
    }

    // ── POST: Bestätigen ──────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostBestaetigenAsync(int zuordnungId)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 2)
            return RedirectToPage("/Zugriff/KeinZugriff");
        if (benutzer.AppRolle == 2)
        {
            var darfZugreifen = await _db.LehrgangPersonen
                .AnyAsync(lp => lp.LehrgangId == LehrgangId
                             && lp.PersonId == benutzer.PersonId
                             && lp.Rolle == LehrgangPersonRolle.Dozent);
            if (!darfZugreifen)
                return RedirectToPage("/Zugriff/KeinZugriff");
        }

        var user = User.Identity?.Name ?? "System";
        try { await _mk.ZuordnungBestaetigenAsync(zuordnungId, user); }
        catch (InvalidOperationException ex)
        {
            await LadeDaten(); Fehler = ex.Message; return Page();
        }
        return RedirectToPage(new { LehrgangId, AbschnittId });
    }

    // ── POST: Buchen ──────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostBuchenAsync(int zuordnungId)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 2)
            return RedirectToPage("/Zugriff/KeinZugriff");
        if (benutzer.AppRolle == 2)
        {
            var darfZugreifen = await _db.LehrgangPersonen
                .AnyAsync(lp => lp.LehrgangId == LehrgangId
                             && lp.PersonId == benutzer.PersonId
                             && lp.Rolle == LehrgangPersonRolle.Dozent);
            if (!darfZugreifen)
                return RedirectToPage("/Zugriff/KeinZugriff");
        }

        var user = User.Identity?.Name ?? "System";
        try { await _mk.ZuordnungBuchenAsync(zuordnungId, user); }
        catch (InvalidOperationException ex)
        {
            await LadeDaten(); Fehler = ex.Message; return Page();
        }
        return RedirectToPage(new { LehrgangId, AbschnittId });
    }

    // ── POST: Zulassung setzen (nur Meisterprüfung) ───────────────────────────

    public async Task<IActionResult> OnPostZulassungAsync(int zuordnungId, string zulassung)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle < 2)
            return RedirectToPage("/Zugriff/KeinZugriff");
        if (benutzer.AppRolle == 2)
        {
            var darfZugreifen = await _db.LehrgangPersonen
                .AnyAsync(lp => lp.LehrgangId == LehrgangId
                             && lp.PersonId == benutzer.PersonId
                             && lp.Rolle == LehrgangPersonRolle.Dozent);
            if (!darfZugreifen)
                return RedirectToPage("/Zugriff/KeinZugriff");
        }

        var z = await _db.MeisterPatientenZuordnungen.FindAsync(zuordnungId);
        if (z != null)
        {
            z.PruefungskommissionZugelassen = zulassung == "ja" ? true : zulassung == "nein" ? false : null;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { LehrgangId, AbschnittId });
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private async Task LadeDaten()
    {
        var zuordnungen = await _db.MeisterPatientenZuordnungen
            .Where(z => z.AbschnittId == AbschnittId)
            .Include(z => z.Termine)
            .OrderBy(z => z.ZuordnungId)
            .ToListAsync();

        Zuordnungen = zuordnungen.Select(z => new ZuordnungZeile(
            z.ZuordnungId, z.PatientPersonId, z.PatientPersonNr, z.PatientName,
            z.Meisterschueler1PersonId, z.Meisterschueler1Nr, z.Meisterschueler1Name,
            z.Meisterschueler2PersonId, z.Meisterschueler2Nr, z.Meisterschueler2Name,
            z.IstErsatzpatient, z.PruefungskommissionZugelassen,
            z.ZuordnungsStatus, z.BuchungsStatus, z.Notiz,
            z.Termine.OrderBy(t => t.TerminTyp).ToList()
        )).ToList();

        // Patientenliste (alle Personen, die als Patient eintragbar sind)
        PatientenListe = await _db.Personen
            .OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .Select(p => new PatientAuswahl(p.PersonId, p.PersonNr,
                p.Nachname + ", " + p.Vorname))
            .ToListAsync();

        // Teilnehmerliste (Meisterschüler dieses Lehrgangs)
        TeilnehmerListe = await _db.LehrgangPersonen
            .Where(lp => lp.LehrgangId == LehrgangId && lp.Rolle == LehrgangPersonRolle.Teilnehmer)
            .OrderBy(lp => lp.Person.Nachname).ThenBy(lp => lp.Person.Vorname)
            .Select(lp => new TeilnehmerAusw(lp.PersonId, lp.Person.PersonNr,
                lp.Person.Nachname + ", " + lp.Person.Vorname))
            .ToListAsync();
    }

    private async Task SpeichereTermine(int zuordnungId, ZuordnungFormular form)
    {
        await AktualisierTermin(zuordnungId, 0, form.T1Datum, form.T1Uhrzeit, form.T1Status, null, null);
        await AktualisierTermin(zuordnungId, 1, form.T2Datum, form.T2Uhrzeit, form.T2Status, null, null);
        await AktualisierTermin(zuordnungId, 2, form.T3Datum, form.T3Uhrzeit, form.T3Status,
            form.T3HilfsmittelUebergeben, form.T3NichtUebergebenGrund);
        await _db.SaveChangesAsync();
    }

    private async Task AktualisierTermin(int zuordnungId, byte typ,
        DateOnly? datum, TimeOnly? uhrzeit, byte status, bool? hilfsmittel, string? grund)
    {
        var t = await _db.MeisterPatientenTermine
            .FirstOrDefaultAsync(x => x.ZuordnungId == zuordnungId && x.TerminTyp == typ);
        if (t == null)
        {
            t = new MeisterPatientenTermin { ZuordnungId = zuordnungId, TerminTyp = typ };
            _db.MeisterPatientenTermine.Add(t);
        }
        t.Datum   = datum;
        t.Uhrzeit = uhrzeit;
        t.Status  = status;
        if (typ == 2) { t.HilfsmittelUebergeben = hilfsmittel; t.NichtUebergebenGrund = grund; }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ZuordnungZeile(
    int ZuordnungId,
    int PatientPersonId, string PatientPersonNr, string PatientName,
    int MS1PersonId, string MS1Nr, string MS1Name,
    int? MS2PersonId, string? MS2Nr, string? MS2Name,
    bool IstErsatz, bool? Zugelassen,
    byte ZuordnungsStatus, byte BuchungsStatus,
    string? Notiz,
    List<MeisterPatientenTermin> Termine);

public record PatientAuswahl(int PersonId, string PersonNr, string Name);
public record TeilnehmerAusw(int PersonId, string PersonNr, string Name);

public class ZuordnungFormular
{
    public int       ZuordnungId             { get; set; }
    public int       PatientPersonId         { get; set; }
    public int       Meisterschueler1PersonId { get; set; }
    public int?      Meisterschueler2PersonId { get; set; }
    public bool      IstErsatzpatient         { get; set; }
    public byte      ZuordnungsStatus         { get; set; }
    public string?   Notiz                    { get; set; }

    // Termine
    public DateOnly? T1Datum   { get; set; }
    public TimeOnly? T1Uhrzeit { get; set; }
    public byte      T1Status  { get; set; }
    public DateOnly? T2Datum   { get; set; }
    public TimeOnly? T2Uhrzeit { get; set; }
    public byte      T2Status  { get; set; }
    public DateOnly? T3Datum   { get; set; }
    public TimeOnly? T3Uhrzeit { get; set; }
    public byte      T3Status  { get; set; }
    public bool?     T3HilfsmittelUebergeben { get; set; }
    public string?   T3NichtUebergebenGrund  { get; set; }
}
