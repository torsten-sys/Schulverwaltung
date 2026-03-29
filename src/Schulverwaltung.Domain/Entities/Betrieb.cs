using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Betrieb – Stammdaten (analog BC Kreditor/Debitor).
/// Ein Betrieb kann mehreren Personen zugeordnet sein.
/// </summary>
public class Betrieb : AuditableEntity
{
    public int     BetriebId           { get; set; }
    public string  BetriebNr           { get; set; } = string.Empty;   // aus NoSerie 'BETRIEB'
    public string  Name                { get; set; } = string.Empty;
    public string? Name2               { get; set; }

    // Kontaktperson
    public int?    AnsprechpartnerPersonId { get; set; }  // FK → Person
    public int?    AusbilderPersonId       { get; set; }  // FK → Person

    // Adresse
    public string? Strasse             { get; set; }
    public string? PLZ                 { get; set; }
    public string? Ort                 { get; set; }
    public string  Land                { get; set; } = "Deutschland";

    // Rechnungsadresse
    public string? RechStrasse         { get; set; }
    public string? RechPLZ             { get; set; }
    public string? RechOrt             { get; set; }
    public string? RechLand            { get; set; }
    public string? RechEmail           { get; set; }
    public string? RechZusatz          { get; set; }

    // Kontakt
    public string? Telefon             { get; set; }
    public string? Email               { get; set; }
    public string? Website             { get; set; }

    // Merkmale
    public bool    IstOrthopaedie              { get; set; } = false;
    public bool    IstPodologie                { get; set; } = false;
    public bool    IstEmailVerteiler           { get; set; } = false;
    public bool    IstFoerdermittelberechtigt  { get; set; } = false;
    public bool    DsgvoCheck                  { get; set; } = false;

    // Organisationen
    public int?    InnungsId          { get; set; }  // FK → Organisation (Typ=Innung)
    public int?    HandwerkskammerId  { get; set; }  // FK → Organisation (Typ=Handwerkskammer)

    // Meta
    public bool    Gesperrt            { get; set; } = false;
    public string? Notiz               { get; set; }

    // Navigation
    public ICollection<PersonRolle> PersonRollen    { get; set; } = new List<PersonRolle>();
    public Person?                  Ansprechpartner { get; set; }
    public Person?                  Ausbilder       { get; set; }
    public Organisation?            Innung          { get; set; }
    public Organisation?            Handwerkskammer { get; set; }
}

/// <summary>
/// Änderungsprotokoll für Betriebe (Snapshot – kein FK zu Betrieb).
/// </summary>
public class BetriebAenderungsposten
{
    public int      PostenId        { get; set; }
    public string   BelegNr         { get; set; } = string.Empty;  // aus NoSerie 'AENDERUNG'
    public string   BetriebNr       { get; set; } = string.Empty;  // Snapshot
    public string   BetriebName     { get; set; } = string.Empty;  // Snapshot
    public string   Ereignis        { get; set; } = string.Empty;
    public string   Tabelle         { get; set; } = string.Empty;
    public string?  Feld            { get; set; }
    public string?  AlterWert       { get; set; }
    public string?  NeuerWert       { get; set; }
    public DateTime Zeitstempel     { get; set; } = DateTime.UtcNow;
    public string   AusfuehrendUser { get; set; } = string.Empty;
    public string?  Notiz           { get; set; }
}
