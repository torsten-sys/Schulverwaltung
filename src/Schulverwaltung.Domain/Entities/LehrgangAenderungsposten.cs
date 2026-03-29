namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Unveränderlicher Änderungsposten für Lehrgänge (Snapshot-Prinzip).
/// Kein UPDATE/DELETE erlaubt – via DB-Trigger geschützt.
/// </summary>
public class LehrgangAenderungsposten
{
    public int      PostenId            { get; set; }
    public string   BelegNr             { get; set; } = string.Empty;
    public int      LehrgangId          { get; set; }  // Snapshot – kein FK
    public string   LehrgangNr          { get; set; } = string.Empty;
    public string   LehrgangBezeichnung { get; set; } = string.Empty;
    public string   Ereignis            { get; set; } = string.Empty;
    public string?  Tabelle             { get; set; }
    public string?  Feld                { get; set; }
    public string?  AlterWert           { get; set; }
    public string?  NeuerWert           { get; set; }
    public DateTime Zeitstempel         { get; set; } = DateTime.UtcNow;
    public string   AusfuehrendUser     { get; set; } = string.Empty;
    public string?  Notiz               { get; set; }
}
