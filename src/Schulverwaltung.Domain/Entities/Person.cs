using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

public enum PersonRolleTyp : byte
{
    Teilnehmer      = 0,
    Dozent          = 1,
    Patient         = 2,
    Ansprechpartner = 3,
    Pruefer         = 4,
    Betreuer        = 5
}

/// <summary>
/// Person – zentrales Stammdatum für alle natürlichen Personen.
/// Ersetzt die früheren Entitäten Teilnehmer und Dozent.
/// </summary>
public class Person : AuditableEntity
{
    public int      PersonId     { get; set; }
    public string   PersonNr     { get; set; } = string.Empty;  // aus NoSerie 'PERSON'
    public string?  Anrede       { get; set; }  // "Herr"/"Frau"/"Divers"
    public string?  Titel        { get; set; }  // "Dr."/"Prof."
    public string   Vorname      { get; set; } = string.Empty;
    public string   Nachname     { get; set; } = string.Empty;
    public string?  Namenszusatz  { get; set; }  // "von"/"van"
    public string?  Geburtsname   { get; set; }  // Geburtsname (z.B. vor Heirat)
    public string?  Geburtsort    { get; set; }
    public string?  Nationalitaet { get; set; }  // default "deutsch"
    public DateOnly? Geburtsdatum { get; set; }
    public byte?    Geschlecht    { get; set; }  // 0=unbekannt 1=männlich 2=weiblich 3=divers

    // Adresse
    public string?  Strasse      { get; set; }
    public string?  PLZ          { get; set; }
    public string?  Ort          { get; set; }
    public string   Land         { get; set; } = "Deutschland";

    // Kontakt
    public string?  Email        { get; set; }
    public string?  Telefon      { get; set; }
    public string?  Mobil        { get; set; }

    // Foto
    public byte[]?  Foto         { get; set; }
    public string?  FotoTyp      { get; set; }  // "image/jpeg" / "image/png"

    // Meta
    public bool     Gesperrt     { get; set; } = false;
    public string?  Notiz        { get; set; }

    // Navigation
    public ICollection<PersonRolle>  Rollen       { get; set; } = new List<PersonRolle>();
    public PersonPatientProfil?      PatientProfil { get; set; }

    // Computed (nicht gespeichert)
    public string VollerName => string.Join(" ",
        new[] { Titel, Vorname, Namenszusatz, Nachname }
        .Where(s => !string.IsNullOrWhiteSpace(s)));

    public string AnzeigeName => $"{Nachname}, {Vorname}";
}

/// <summary>
/// Rolle einer Person (Teilnehmer, Dozent, …).
/// Unique Constraint: (PersonId, RolleTyp) wo Status=Aktiv.
/// </summary>
public class PersonRolle : AuditableEntity
{
    public int            PersonRolleId { get; set; }
    public int            PersonId      { get; set; }
    public PersonRolleTyp RolleTyp      { get; set; }
    public byte           Status        { get; set; } = 0;  // 0=Aktiv, 1=Inaktiv
    public DateOnly       GueltigAb     { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly?      GueltigBis    { get; set; }
    public int?           BetriebId     { get; set; }  // Betrieb pro Rolle
    public string?        Notiz         { get; set; }

    // Navigation
    public Person   Person  { get; set; } = null!;
    public Betrieb? Betrieb { get; set; }
}

/// <summary>
/// Erweiterungsdaten für Personen in der Rolle Dozent.
/// PersonId ist gleichzeitig PK und FK auf Person (1:1).
/// </summary>
public class PersonDozentProfil : AuditableEntity
{
    public int      PersonId              { get; set; }  // PK + FK
    public string?  Kuerzel               { get; set; }
    public bool     Intern                { get; set; } = true;
    public decimal? MaxStundenProWoche    { get; set; }
    public string?  Bemerkungen           { get; set; }

    // Fachbereiche
    public bool     IstOrthopaedie        { get; set; } = false;
    public bool     IstPodologie          { get; set; } = false;
    public bool     IstMedizin            { get; set; } = false;

    // Vergütung
    public decimal? KostenTheoriestunde   { get; set; }
    public decimal? KostenPraxisstunde    { get; set; }
    public decimal? Fahrtkosten           { get; set; }

    // Bankverbindung
    public string?  IBAN                  { get; set; }  // max 34 Zeichen, roh ohne Leerzeichen

    // Navigation
    public Person Person { get; set; } = null!;
}

/// <summary>
/// Erweiterungsdaten für Personen in der Rolle Patient (Körperdaten, Eignungsmerkmale).
/// PersonId ist gleichzeitig PK und FK auf Person (1:1).
/// </summary>
public class PersonPatientProfil : AuditableEntity
{
    public int      PersonId                     { get; set; }  // PK + FK

    // Körperdaten
    public int?     Groesse                      { get; set; }   // cm
    public decimal? Gewicht                      { get; set; }   // kg, decimal(5,1)

    // Medizinische Hinweise
    public bool     IstDiabetiker                { get; set; } = false;

    // Eignung Patientenversorgung
    public bool     GeeignetPV1                  { get; set; } = false;
    public bool     GeeignetPV2                  { get; set; } = false;
    public bool     GeeignetPV3                  { get; set; } = false;
    public bool     GeeignetPV4                  { get; set; } = false;
    public bool     GeeignetPVPruefung           { get; set; } = false;

    // Bemerkungen
    public string?  Bemerkungen                  { get; set; }

    // Navigation
    public Person   Person                       { get; set; } = null!;
}
