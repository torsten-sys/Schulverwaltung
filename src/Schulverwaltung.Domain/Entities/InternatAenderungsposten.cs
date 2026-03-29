namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Unveränderlicher Änderungsposten für Internat (Snapshot-Prinzip).
/// Kein UPDATE/DELETE erlaubt – via DB-Trigger geschützt.
/// </summary>
public class InternatAenderungsposten
{
    public int      PostenId          { get; set; }
    public string   BelegNr           { get; set; } = string.Empty;
    public int      ZimmerId          { get; set; }         // Snapshot – kein FK
    public string   ZimmerNr          { get; set; } = string.Empty;
    public string   ZimmerBezeichnung { get; set; } = string.Empty;
    public int?     BelegungId        { get; set; }         // Snapshot – kein FK
    public string?  PersonNr          { get; set; }
    public string?  PersonName        { get; set; }
    public string   Ereignis          { get; set; } = string.Empty;
    public string?  Tabelle           { get; set; }
    public string?  Feld              { get; set; }
    public string?  AlterWert         { get; set; }
    public string?  NeuerWert         { get; set; }
    public DateTime Zeitstempel       { get; set; } = DateTime.UtcNow;
    public string   AusfuehrendUser   { get; set; } = string.Empty;
    public string?  Notiz             { get; set; }
}
