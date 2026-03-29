using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Persistence;
using System.ComponentModel.DataAnnotations;

namespace Schulverwaltung.Web.Pages.Betriebe;

public class KarteModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public KarteModel(SchulverwaltungDbContext db) => _db = db;

    [BindProperty] public BetriebFormular Form { get; set; } = new();

    public bool   IstNeu => Form.BetriebId == 0;
    public string Titel  => IstNeu ? "Neuer Betrieb" : $"{Form.BetriebNr} · {Form.Name}";

    public List<BetriebPersonZeile>           Personen         { get; set; } = [];
    public List<BetriebLehrgangZeile>         Lehrgaenge       { get; set; } = [];
    public List<BetriebAenderungspostenZeile> Aenderungsposten { get; set; } = [];
    public List<AnhangZeile>                  Anhaenge         { get; set; } = [];
    public BetriebStatistik                   Statistik        { get; set; } = new();
    public List<PersonAuswahl>                PersonenListe    { get; set; } = [];
    public List<OrganisationAuswahl>          InnungsListe     { get; set; } = [];
    public List<OrganisationAuswahl>          HwkListe         { get; set; } = [];

    // ── GET ───────────────────────────────────────────────────────────────────

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer == null || benutzer.AppRolle == 0)
            return RedirectToPage("/Zugriff/KeinZugriff");

        await LadePersonenListe();
        await LadeOrganisationsListen();

        if (id == null || id == 0)
        {
            Form = new BetriebFormular { Land = "Deutschland" };
            return Page();
        }

        var b = await _db.Betriebe.FirstOrDefaultAsync(x => x.BetriebId == id);
        if (b == null) return NotFound();

        Form = MapToForm(b);

        await LadePersonen(b.BetriebId);
        await LadeLehrgaenge(b.BetriebId);
        await LadeAenderungsposten(b.BetriebId);
        await LadeAnhaenge(b.BetriebId);
        await LadeStatistik(b.BetriebId);

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
            await LadePersonenListe();
            await LadeOrganisationsListen();
            if (Form.BetriebId != 0)
            {
                await LadePersonen(Form.BetriebId);
                await LadeLehrgaenge(Form.BetriebId);
                await LadeAenderungsposten(Form.BetriebId);
                await LadeAnhaenge(Form.BetriebId);
                await LadeStatistik(Form.BetriebId);
            }
            return Page();
        }

        var user = User.Identity?.Name ?? "System";

        if (Form.BetriebId == 0)
        {
            var nr = await NextNoAsync("BETRIEB");
            var betrieb = new Betrieb
            {
                BetriebNr                  = nr,
                Name                       = Form.Name,
                Name2                      = Form.Name2,
                AnsprechpartnerPersonId    = Form.AnsprechpartnerPersonId,
                AusbilderPersonId          = Form.AusbilderPersonId,
                Strasse                    = Form.Strasse,
                PLZ                        = Form.PLZ,
                Ort                        = Form.Ort,
                Land                       = Form.Land ?? "Deutschland",
                RechStrasse                = Form.RechStrasse,
                RechPLZ                    = Form.RechPLZ,
                RechOrt                    = Form.RechOrt,
                RechLand                   = Form.RechLand,
                RechEmail                  = Form.RechEmail,
                RechZusatz                 = Form.RechZusatz,
                Telefon                    = Form.Telefon,
                Email                      = Form.Email,
                Website                    = Form.Website,
                IstOrthopaedie             = Form.IstOrthopaedie,
                IstPodologie               = Form.IstPodologie,
                IstEmailVerteiler          = Form.IstEmailVerteiler,
                IstFoerdermittelberechtigt = Form.IstFoerdermittelberechtigt,
                DsgvoCheck                 = Form.DsgvoCheck,
                Gesperrt                   = Form.Gesperrt,
                Notiz                      = Form.Notiz,
                InnungsId                  = Form.InnungsId,
                HandwerkskammerId          = Form.HandwerkskammerId
            };
            _db.Betriebe.Add(betrieb);
            await _db.SaveChangesAsync();

            // Ansprechpartner-Rolle automatisch zuweisen
            if (betrieb.AnsprechpartnerPersonId.HasValue)
                await SicherPersonRolleAnsprechpartner(betrieb.AnsprechpartnerPersonId.Value, betrieb.BetriebId, user);

            // Änderungsposten: Angelegt
            var belegNr = await NextNoAsync("AENDERUNG");
            AenderungspostenSchreiben(belegNr, betrieb.BetriebNr, betrieb.Name,
                "BetriebAngelegt", "Betrieb", null, null, betrieb.BetriebNr, user);
            await _db.SaveChangesAsync();

            return RedirectToPage(new { id = betrieb.BetriebId });
        }
        else
        {
            var b = await _db.Betriebe
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BetriebId == Form.BetriebId);
            if (b == null) return NotFound();

            var tracked = await _db.Betriebe.FindAsync(Form.BetriebId);
            if (tracked == null) return NotFound();

            // Feldänderungen verfolgen
            var belegNr = await NextNoAsync("AENDERUNG");

            void Check(string feld, string? alt, string? neu)
            {
                if (string.Equals(alt?.Trim(), neu?.Trim(), StringComparison.OrdinalIgnoreCase)) return;
                AenderungspostenSchreiben(belegNr, b.BetriebNr, b.Name,
                    "StammdatenGeaendert", "Betrieb", feld, alt, neu, user);
            }
            void CheckBool(string feld, bool alt, bool neu)
            {
                if (alt == neu) return;
                AenderungspostenSchreiben(belegNr, b.BetriebNr, b.Name,
                    "StammdatenGeaendert", "Betrieb", feld,
                    alt ? "Ja" : "Nein", neu ? "Ja" : "Nein", user);
            }

            Check("Name",              b.Name,           Form.Name);
            Check("Name 2",            b.Name2,          Form.Name2);
            Check("Straße",            b.Strasse,        Form.Strasse);
            Check("PLZ",               b.PLZ,            Form.PLZ);
            Check("Ort",               b.Ort,            Form.Ort);
            Check("Land",              b.Land,           Form.Land);
            Check("Rech. Straße",      b.RechStrasse,    Form.RechStrasse);
            Check("Rech. PLZ",         b.RechPLZ,        Form.RechPLZ);
            Check("Rech. Ort",         b.RechOrt,        Form.RechOrt);
            Check("Rech. Land",        b.RechLand,       Form.RechLand);
            Check("Rech. E-Mail",      b.RechEmail,      Form.RechEmail);
            Check("Rech. Zusatz",      b.RechZusatz,     Form.RechZusatz);
            Check("Telefon",           b.Telefon,        Form.Telefon);
            Check("E-Mail",            b.Email,          Form.Email);
            Check("Website",           b.Website,        Form.Website);
            Check("Notiz",             b.Notiz,          Form.Notiz);
            CheckBool("Orthopädie",    b.IstOrthopaedie,             Form.IstOrthopaedie);
            CheckBool("Podologie",     b.IstPodologie,               Form.IstPodologie);
            CheckBool("E-Mail-Verteiler", b.IstEmailVerteiler,       Form.IstEmailVerteiler);
            CheckBool("Fördermittelberechtigt", b.IstFoerdermittelberechtigt, Form.IstFoerdermittelberechtigt);
            CheckBool("DSGVO-Check",   b.DsgvoCheck,                 Form.DsgvoCheck);

            if (b.Gesperrt != Form.Gesperrt)
            {
                var ereignis = Form.Gesperrt ? "Gesperrt" : "Entsperrt";
                AenderungspostenSchreiben(belegNr, b.BetriebNr, b.Name,
                    ereignis, "Betrieb", "Gesperrt",
                    b.Gesperrt ? "Ja" : "Nein", Form.Gesperrt ? "Ja" : "Nein", user);
            }

            // Ansprechpartner geändert?
            if (b.AnsprechpartnerPersonId != Form.AnsprechpartnerPersonId)
            {
                var altName = b.AnsprechpartnerPersonId.HasValue
                    ? await _db.Personen.Where(p => p.PersonId == b.AnsprechpartnerPersonId)
                        .Select(p => p.Nachname + ", " + p.Vorname).FirstOrDefaultAsync()
                    : null;
                var neuName = Form.AnsprechpartnerPersonId.HasValue
                    ? await _db.Personen.Where(p => p.PersonId == Form.AnsprechpartnerPersonId)
                        .Select(p => p.Nachname + ", " + p.Vorname).FirstOrDefaultAsync()
                    : null;
                AenderungspostenSchreiben(belegNr, b.BetriebNr, b.Name,
                    "AnsprechpartnerGeaendert", "Betrieb", "Ansprechpartner", altName, neuName, user);

                if (Form.AnsprechpartnerPersonId.HasValue)
                    await SicherPersonRolleAnsprechpartner(Form.AnsprechpartnerPersonId.Value, b.BetriebId, user);
            }

            // Ausbilder geändert?
            if (b.AusbilderPersonId != Form.AusbilderPersonId)
            {
                var altName = b.AusbilderPersonId.HasValue
                    ? await _db.Personen.Where(p => p.PersonId == b.AusbilderPersonId)
                        .Select(p => p.Nachname + ", " + p.Vorname).FirstOrDefaultAsync()
                    : null;
                var neuName = Form.AusbilderPersonId.HasValue
                    ? await _db.Personen.Where(p => p.PersonId == Form.AusbilderPersonId)
                        .Select(p => p.Nachname + ", " + p.Vorname).FirstOrDefaultAsync()
                    : null;
                AenderungspostenSchreiben(belegNr, b.BetriebNr, b.Name,
                    "StammdatenGeaendert", "Betrieb", "Ausbilder", altName, neuName, user);
            }

            // Innung geändert?
            if (b.InnungsId != Form.InnungsId)
            {
                var altOrg = b.InnungsId.HasValue
                    ? await _db.Organisationen.Where(o => o.OrganisationId == b.InnungsId)
                        .Select(o => o.OrganisationsNr + " " + o.Name).FirstOrDefaultAsync()
                    : null;
                var neuOrg = Form.InnungsId.HasValue
                    ? await _db.Organisationen.Where(o => o.OrganisationId == Form.InnungsId)
                        .Select(o => o.OrganisationsNr + " " + o.Name).FirstOrDefaultAsync()
                    : null;
                AenderungspostenSchreiben(belegNr, b.BetriebNr, b.Name,
                    "StammdatenGeaendert", "Betrieb", "Innung", altOrg, neuOrg, user);
            }

            // Handwerkskammer geändert?
            if (b.HandwerkskammerId != Form.HandwerkskammerId)
            {
                var altOrg = b.HandwerkskammerId.HasValue
                    ? await _db.Organisationen.Where(o => o.OrganisationId == b.HandwerkskammerId)
                        .Select(o => o.OrganisationsNr + " " + o.Name).FirstOrDefaultAsync()
                    : null;
                var neuOrg = Form.HandwerkskammerId.HasValue
                    ? await _db.Organisationen.Where(o => o.OrganisationId == Form.HandwerkskammerId)
                        .Select(o => o.OrganisationsNr + " " + o.Name).FirstOrDefaultAsync()
                    : null;
                AenderungspostenSchreiben(belegNr, b.BetriebNr, b.Name,
                    "StammdatenGeaendert", "Betrieb", "Handwerkskammer", altOrg, neuOrg, user);
            }

            // Betrieb aktualisieren
            tracked.Name                       = Form.Name;
            tracked.Name2                      = Form.Name2;
            tracked.AnsprechpartnerPersonId    = Form.AnsprechpartnerPersonId;
            tracked.AusbilderPersonId          = Form.AusbilderPersonId;
            tracked.Strasse                    = Form.Strasse;
            tracked.PLZ                        = Form.PLZ;
            tracked.Ort                        = Form.Ort;
            tracked.Land                       = Form.Land ?? "Deutschland";
            tracked.RechStrasse                = Form.RechStrasse;
            tracked.RechPLZ                    = Form.RechPLZ;
            tracked.RechOrt                    = Form.RechOrt;
            tracked.RechLand                   = Form.RechLand;
            tracked.RechEmail                  = Form.RechEmail;
            tracked.RechZusatz                 = Form.RechZusatz;
            tracked.Telefon                    = Form.Telefon;
            tracked.Email                      = Form.Email;
            tracked.Website                    = Form.Website;
            tracked.IstOrthopaedie             = Form.IstOrthopaedie;
            tracked.IstPodologie               = Form.IstPodologie;
            tracked.IstEmailVerteiler          = Form.IstEmailVerteiler;
            tracked.IstFoerdermittelberechtigt = Form.IstFoerdermittelberechtigt;
            tracked.DsgvoCheck                 = Form.DsgvoCheck;
            tracked.Gesperrt                   = Form.Gesperrt;
            tracked.Notiz                      = Form.Notiz;
            tracked.InnungsId                  = Form.InnungsId;
            tracked.HandwerkskammerId          = Form.HandwerkskammerId;

            await _db.SaveChangesAsync();
            return RedirectToPage(new { id = tracked.BetriebId });
        }
    }

    // ── POST: Löschen ─────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostLoeschenAsync(int id)
    {
        var b = await _db.Betriebe.FirstOrDefaultAsync(x => x.BetriebId == id);
        if (b == null) return NotFound();

        var hatPersonen = await _db.PersonRollen.AnyAsync(r => r.BetriebId == id && r.Status == 0);
        if (hatPersonen)
        {
            ModelState.AddModelError("", "Betrieb kann nicht gelöscht werden, da noch Personen zugeordnet sind.");
            Form = MapToForm(b);
            await LadePersonenListe();
            await LadePersonen(id);
            await LadeLehrgaenge(id);
            await LadeAenderungsposten(id);
            await LadeAnhaenge(id);
            await LadeStatistik(id);
            return Page();
        }

        _db.Betriebe.Remove(b);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    // ── POST: Anhang hochladen ────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAnhangHochladenAsync(
        int betriebId, string bezeichnung, IFormFile? datei)
    {
        if (datei == null || datei.Length == 0 || string.IsNullOrWhiteSpace(bezeichnung))
            return RedirectToPage(new { id = betriebId });

        if (datei.Length > 10 * 1024 * 1024)
        {
            ModelState.AddModelError("", "Datei darf max. 10 MB groß sein.");
            return RedirectToPage(new { id = betriebId });
        }

        using var ms = new MemoryStream();
        await datei.CopyToAsync(ms);

        var anhang = new Anhang
        {
            BezugTyp       = "Betrieb",
            BezugId        = betriebId,
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

        return RedirectToPage(new { id = betriebId });
    }

    // ── POST: Anhang löschen ──────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAnhangLoeschenAsync(int anhangId, int betriebId)
    {
        var a = await _db.Anhaenge.FindAsync(anhangId);
        if (a != null)
        {
            _db.Anhaenge.Remove(a);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { id = betriebId });
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private async Task LadePersonen(int betriebId)
    {
        var rollen = await _db.PersonRollen
            .Where(r => r.BetriebId == betriebId && r.Status == 0)
            .Include(r => r.Person)
            .OrderBy(r => r.RolleTyp).ThenBy(r => r.Person.Nachname).ThenBy(r => r.Person.Vorname)
            .ToListAsync();

        Personen = rollen.Select(r => new BetriebPersonZeile(
            r.PersonId, r.Person.PersonNr, r.Person.Nachname, r.Person.Vorname,
            r.RolleTyp.ToString(), r.Person.Email, r.Person.Telefon
        )).ToList();
    }

    private async Task LadeLehrgaenge(int betriebId)
    {
        // Lehrgänge über Personen, die aktiv diesem Betrieb zugeordnet sind
        var personIds = await _db.PersonRollen
            .Where(r => r.BetriebId == betriebId && r.Status == 0)
            .Select(r => r.PersonId)
            .Distinct()
            .ToListAsync();

        if (personIds.Count == 0) return;

        var lehrgaenge = await _db.Set<LehrgangPerson>()
            .Where(lp => personIds.Contains(lp.PersonId))
            .Include(lp => lp.Lehrgang)
            .GroupBy(lp => lp.LehrgangId)
            .Select(g => g.First().Lehrgang)
            .OrderByDescending(l => l.StartDatum)
            .ToListAsync();

        Lehrgaenge = lehrgaenge.Select(l => new BetriebLehrgangZeile(
            l.LehrgangId, l.LehrgangNr, l.Bezeichnung,
            l.StartDatum, l.EndDatum, l.Status.ToString()
        )).ToList();
    }

    private async Task LadeAenderungsposten(int betriebId)
    {
        var b = await _db.Betriebe.AsNoTracking()
            .Where(x => x.BetriebId == betriebId)
            .Select(x => x.BetriebNr)
            .FirstOrDefaultAsync();
        if (b == null) return;

        var posten = await _db.BetriebAenderungsposten
            .Where(p => p.BetriebNr == b)
            .OrderByDescending(p => p.Zeitstempel)
            .ToListAsync();

        Aenderungsposten = posten.Select(p => new BetriebAenderungspostenZeile(
            p.PostenId, p.BelegNr, p.Ereignis, p.Tabelle, p.Feld,
            p.AlterWert, p.NeuerWert, p.Zeitstempel, p.AusfuehrendUser, p.Notiz
        )).ToList();
    }

    private async Task LadeAnhaenge(int betriebId)
    {
        var liste = await _db.Anhaenge
            .Where(a => a.BezugTyp == "Betrieb" && a.BezugId == betriebId)
            .OrderByDescending(a => a.HochgeladenAm)
            .ToListAsync();

        Anhaenge = liste.Select(a => new AnhangZeile(
            a.AnhangId, a.Bezeichnung, a.DateiName, a.DateiTyp,
            a.DateiGroesse, a.HochgeladenVon, a.HochgeladenAm
        )).ToList();
    }

    private async Task LadeStatistik(int betriebId)
    {
        var rollenGruppen = await _db.PersonRollen
            .Where(r => r.BetriebId == betriebId && r.Status == 0)
            .GroupBy(r => r.RolleTyp)
            .Select(g => new { Rolle = g.Key, Anzahl = g.Count() })
            .ToListAsync();

        Statistik = new BetriebStatistik
        {
            AnzahlPersonen    = rollenGruppen.Sum(g => g.Anzahl),
            AnzahlTeilnehmer  = rollenGruppen.FirstOrDefault(g => g.Rolle == PersonRolleTyp.Teilnehmer)?.Anzahl ?? 0,
            AnzahlDozenten    = rollenGruppen.FirstOrDefault(g => g.Rolle == PersonRolleTyp.Dozent)?.Anzahl ?? 0,
            AnzahlLehrgaenge  = Lehrgaenge.Count
        };
    }

    private async Task LadePersonenListe()
    {
        PersonenListe = await _db.Personen
            .Where(p => !p.Gesperrt)
            .OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .Select(p => new PersonAuswahl(p.PersonId, p.PersonNr, p.Nachname + ", " + p.Vorname))
            .ToListAsync();
    }

    private async Task LadeOrganisationsListen()
    {
        InnungsListe = await _db.Organisationen
            .Where(o => o.OrganisationsTyp == 0 && !o.Gesperrt)
            .OrderBy(o => o.Name)
            .Select(o => new OrganisationAuswahl(o.OrganisationId, o.OrganisationsNr,
                o.Name + (o.Kurzbezeichnung != null ? " (" + o.Kurzbezeichnung + ")" : "")))
            .ToListAsync();

        HwkListe = await _db.Organisationen
            .Where(o => o.OrganisationsTyp == 1 && !o.Gesperrt)
            .OrderBy(o => o.Name)
            .Select(o => new OrganisationAuswahl(o.OrganisationId, o.OrganisationsNr,
                o.Name + (o.Kurzbezeichnung != null ? " (" + o.Kurzbezeichnung + ")" : "")))
            .ToListAsync();
    }

    private async Task SicherPersonRolleAnsprechpartner(int personId, int betriebId, string user)
    {
        var hatRolle = await _db.PersonRollen.AnyAsync(r =>
            r.PersonId == personId &&
            r.RolleTyp == PersonRolleTyp.Ansprechpartner &&
            r.BetriebId == betriebId &&
            r.Status == 0);

        if (!hatRolle)
        {
            _db.PersonRollen.Add(new PersonRolle
            {
                PersonId  = personId,
                RolleTyp  = PersonRolleTyp.Ansprechpartner,
                BetriebId = betriebId,
                GueltigAb = DateOnly.FromDateTime(DateTime.Today),
                Status    = 0
            });
        }
    }

    private void AenderungspostenSchreiben(
        string belegNr, string betriebNr, string betriebName,
        string ereignis, string tabelle, string? feld,
        string? alterWert, string? neuerWert, string user)
    {
        _db.BetriebAenderungsposten.Add(new BetriebAenderungsposten
        {
            BelegNr        = belegNr,
            BetriebNr      = betriebNr,
            BetriebName    = betriebName,
            Ereignis       = ereignis,
            Tabelle        = tabelle,
            Feld           = feld,
            AlterWert      = alterWert,
            NeuerWert      = neuerWert,
            Zeitstempel    = DateTime.UtcNow,
            AusfuehrendUser = user
        });
    }

    private static BetriebFormular MapToForm(Betrieb b) => new()
    {
        BetriebId                  = b.BetriebId,
        BetriebNr                  = b.BetriebNr,
        Name                       = b.Name,
        Name2                      = b.Name2,
        AnsprechpartnerPersonId    = b.AnsprechpartnerPersonId,
        AusbilderPersonId          = b.AusbilderPersonId,
        Strasse                    = b.Strasse,
        PLZ                        = b.PLZ,
        Ort                        = b.Ort,
        Land                       = b.Land,
        RechStrasse                = b.RechStrasse,
        RechPLZ                    = b.RechPLZ,
        RechOrt                    = b.RechOrt,
        RechLand                   = b.RechLand,
        RechEmail                  = b.RechEmail,
        RechZusatz                 = b.RechZusatz,
        Telefon                    = b.Telefon,
        Email                      = b.Email,
        Website                    = b.Website,
        IstOrthopaedie             = b.IstOrthopaedie,
        IstPodologie               = b.IstPodologie,
        IstEmailVerteiler          = b.IstEmailVerteiler,
        IstFoerdermittelberechtigt = b.IstFoerdermittelberechtigt,
        DsgvoCheck                 = b.DsgvoCheck,
        Gesperrt                   = b.Gesperrt,
        Notiz                      = b.Notiz,
        InnungsId                  = b.InnungsId,
        HandwerkskammerId          = b.HandwerkskammerId
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

public record BetriebPersonZeile(
    int PersonId, string PersonNr, string Nachname, string Vorname,
    string Rolle, string? Email, string? Telefon);

public record BetriebLehrgangZeile(
    int LehrgangId, string LehrgangNr, string Bezeichnung,
    DateOnly StartDatum, DateOnly? EndDatum, string Status);

public record BetriebAenderungspostenZeile(
    int PostenId, string BelegNr, string Ereignis, string Tabelle,
    string? Feld, string? AlterWert, string? NeuerWert,
    DateTime Zeitstempel, string User, string? Notiz);

public record AnhangZeile(
    int AnhangId, string Bezeichnung, string DateiName, string DateiTyp,
    int DateiGroesse, string? HochgeladenVon, DateTime HochgeladenAm);

public record PersonAuswahl(int PersonId, string PersonNr, string AnzeigeName);

public record OrganisationAuswahl(int OrganisationId, string OrganisationsNr, string AnzeigeName);

public class BetriebStatistik
{
    public int AnzahlPersonen   { get; set; }
    public int AnzahlTeilnehmer { get; set; }
    public int AnzahlDozenten   { get; set; }
    public int AnzahlLehrgaenge { get; set; }
}

public class BetriebFormular
{
    public int     BetriebId                  { get; set; }
    public string  BetriebNr                  { get; set; } = "";
    [Required(ErrorMessage = "Name ist erforderlich")]
    public string  Name                       { get; set; } = "";
    public string? Name2                      { get; set; }
    public int?    AnsprechpartnerPersonId    { get; set; }
    public int?    AusbilderPersonId          { get; set; }
    public string? Strasse                    { get; set; }
    public string? PLZ                        { get; set; }
    public string? Ort                        { get; set; }
    public string? Land                       { get; set; }
    public string? RechStrasse                { get; set; }
    public string? RechPLZ                    { get; set; }
    public string? RechOrt                    { get; set; }
    public string? RechLand                   { get; set; }
    public string? RechEmail                  { get; set; }
    public string? RechZusatz                 { get; set; }
    public string? Telefon                    { get; set; }
    public string? Email                      { get; set; }
    public string? Website                    { get; set; }
    public bool    IstOrthopaedie             { get; set; }
    public bool    IstPodologie               { get; set; }
    public bool    IstEmailVerteiler          { get; set; }
    public bool    IstFoerdermittelberechtigt { get; set; }
    public bool    DsgvoCheck                 { get; set; }
    public bool    Gesperrt                   { get; set; }
    public string? Notiz                      { get; set; }
    public int?    InnungsId                  { get; set; }
    public int?    HandwerkskammerId          { get; set; }
}
