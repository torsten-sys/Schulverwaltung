using Schulverwaltung.Domain.Common;
using Schulverwaltung.Domain.Enums;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Lehrgang – zentrales Stammdatum (analog BC Auftrag/Order).
/// </summary>
public class Lehrgang : AuditableEntity
{
    public int            LehrgangId      { get; set; }
    public string         LehrgangNr      { get; set; } = string.Empty;  // aus NoSerie 'LEHRGANG'
    public LehrgangTyp    LehrgangTyp     { get; set; } = LehrgangTyp.Meistervorbereitung;
    public string         Bezeichnung     { get; set; } = string.Empty;  // Kurzbezeichnung
    public string?        BezeichnungLang { get; set; }
    public string?        Beschreibung    { get; set; }

    // Lehrgangsart (frei konfigurierbar)
    public int?           ArtId           { get; set; }

    // Zeitraum
    public DateOnly       StartDatum      { get; set; }
    public DateOnly?      EndDatum        { get; set; }

    // Kapazität (0 = unbegrenzt)
    public int            MinTeilnehmer   { get; set; } = 0;
    public int            MaxTeilnehmer   { get; set; } = 0;

    // Gebühren (allgemein)
    public decimal?       Gebuehren       { get; set; }

    // Meisterkurs: Kosten-Felder (nur relevant bei LehrgangTyp == 0)
    public decimal?  KostenLehrgang       { get; set; }
    public decimal?  KostenInternatDZ     { get; set; }
    public decimal?  KostenInternatEZ     { get; set; }
    public decimal?  GrundzahlungBetrag   { get; set; }
    public DateOnly? GrundzahlungTermin   { get; set; }
    public DateOnly? BeginnAbbuchung      { get; set; }
    public decimal?  KautionWerkstatt     { get; set; }
    public decimal?  KautionInternat      { get; set; }
    public decimal?  Verwaltungspauschale { get; set; }
    public int?      AnzahlRaten          { get; set; }  // Anzahl monatlicher Raten (Standard: 6)

    // Status
    public LehrgangStatus Status          { get; set; } = LehrgangStatus.Planung;

    // Meta
    public string?        Notiz           { get; set; }
    public string?        CreatedBy       { get; set; }
    public string?        ModifiedBy      { get; set; }

    // Navigation
    public LehrgangArt?                Art       { get; set; }
    public ICollection<LehrgangPerson> Personen  { get; set; } = new List<LehrgangPerson>();
    public ICollection<LehrgangEinheit> Einheiten { get; set; } = new List<LehrgangEinheit>();

    // Meisterkurs-spezifisch (nur aktiv wenn LehrgangTyp == 0)
    public ICollection<MeisterAbschnitt>  MeisterAbschnitte  { get; set; } = new List<MeisterAbschnitt>();
    public ICollection<MeisterFach>       MeisterFaecher     { get; set; } = new List<MeisterFach>();
    public ICollection<MeisterFunktion>   MeisterFunktionen  { get; set; } = new List<MeisterFunktion>();

    // Domain-Logik
    public bool IstVoll     => MaxTeilnehmer > 0 && Personen.Count(p => p.Rolle == LehrgangPersonRolle.Teilnehmer && p.Status == 1) >= MaxTeilnehmer;
    public int  FreiePlaetze => MaxTeilnehmer == 0 ? int.MaxValue : MaxTeilnehmer - Personen.Count(p => p.Rolle == LehrgangPersonRolle.Teilnehmer && p.Status == 1);
}

/// <summary>
/// Verknüpfung Lehrgang ↔ Person. PK: (LehrgangId, PersonId, Rolle) –
/// dieselbe Person kann gleichzeitig Teilnehmer und Dozent sein.
/// </summary>
public class LehrgangPerson : AuditableEntity
{
    public int                LehrgangId      { get; set; }
    public int                PersonId        { get; set; }
    public LehrgangPersonRolle Rolle          { get; set; } = LehrgangPersonRolle.Teilnehmer;

    // Status: 0=Warteliste 1=Angemeldet 2=Abgemeldet 3=Bestanden
    public byte               Status          { get; set; } = 1;
    public DateOnly           AnmeldungsDatum { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public decimal?           GeplanteStunden { get; set; }
    public string?            Notiz           { get; set; }

    // Betrieb-Snapshot (denormalisiert – bleibt erhalten wenn Betrieb gelöscht wird)
    public int?               BetriebId       { get; set; }
    public string?            BetriebName     { get; set; }

    // Navigation
    public Lehrgang Lehrgang { get; set; } = null!;
    public Person   Person   { get; set; } = null!;
}
