namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Unveränderlicher Audit-Eintrag für Benutzeraktionen (Snapshot-Prinzip).
/// Kein FK – Trigger-geschützt gegen UPDATE/DELETE.
/// </summary>
public class AppBenutzerAenderungsposten
{
    public int      PostenId        { get; set; }
    public string   BelegNr         { get; set; } = string.Empty;

    // Snapshot
    public int      BenutzerId      { get; set; }
    public string   AdBenutzername  { get; set; } = string.Empty;
    public string?  DisplayName     { get; set; }

    /// <summary>ErsterLogin | RolleGeaendert | Gesperrt | Entsperrt | PersonVerknuepft | PersonGeloest</summary>
    public string   Ereignis        { get; set; } = string.Empty;

    public string?  Tabelle         { get; set; }
    public string?  Feld            { get; set; }
    public string?  AlterWert       { get; set; }
    public string?  NeuerWert       { get; set; }

    public DateTime Zeitstempel     { get; set; } = DateTime.UtcNow;
    public string   AusfuehrendUser { get; set; } = string.Empty;
    public string?  Notiz           { get; set; }
}
