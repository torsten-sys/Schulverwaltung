using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Organisation – Innungen und Handwerkskammern.
/// Betriebe können einer Innung (InnungsId) und/oder einer Handwerkskammer (HandwerkskammerId) zugeordnet werden.
/// </summary>
public class Organisation : AuditableEntity
{
    public int      OrganisationId                  { get; set; }
    public string   OrganisationsNr                 { get; set; } = string.Empty;  // aus NoSerie 'ORG'
    public byte     OrganisationsTyp                { get; set; }                  // 0=Innung, 1=Handwerkskammer
    public string   Name                            { get; set; } = string.Empty;
    public string?  Kurzbezeichnung                 { get; set; }

    // Adresse
    public string?  Strasse                         { get; set; }
    public string?  PLZ                             { get; set; }
    public string?  Ort                             { get; set; }
    public string   Land                            { get; set; } = "Deutschland";

    // Kontakt
    public string?  Telefon                         { get; set; }
    public string?  Email                           { get; set; }
    public string?  Website                         { get; set; }

    // Vereinbarungen
    public decimal? VereinbarteUebernachtungskosten { get; set; }
    public bool     Sammelrechnung                  { get; set; } = false;

    // Meta
    public bool     Gesperrt                        { get; set; } = false;
    public string?  Notiz                           { get; set; }

    // Navigation: Betriebe über InnungsId
    public ICollection<Betrieb> Betriebe        { get; set; } = new List<Betrieb>();
    // Navigation: Betriebe über HandwerkskammerId
    public ICollection<Betrieb> Kammerbetriebe  { get; set; } = new List<Betrieb>();
}

/// <summary>
/// Unveränderlicher Änderungsposten für Organisationen (Snapshot-Prinzip).
/// Kein UPDATE/DELETE erlaubt – via DB-Trigger geschützt.
/// </summary>
public class OrganisationAenderungsposten
{
    public int      PostenId          { get; set; }  // PK, IDENTITY
    public string   BelegNr           { get; set; } = string.Empty;
    public int      OrganisationId    { get; set; }  // Snapshot – kein FK
    public string   OrganisationsNr   { get; set; } = string.Empty;
    public string   OrganisationsName { get; set; } = string.Empty;
    public string   Ereignis          { get; set; } = string.Empty;
    public string?  Tabelle           { get; set; }
    public string?  Feld              { get; set; }
    public string?  AlterWert         { get; set; }
    public string?  NeuerWert         { get; set; }
    public DateTime Zeitstempel       { get; set; } = DateTime.UtcNow;
    public string   AusfuehrendUser   { get; set; } = string.Empty;
    public string?  Notiz             { get; set; }
}
