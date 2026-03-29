namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Unveränderlicher Audit-Eintrag für Notenänderungen.
/// Kein FK (Snapshot-Prinzip). Trigger-geschützt gegen UPDATE/DELETE.
/// </summary>
public class MeisterNoteAenderungsposten
{
    public int      PostenId            { get; set; }
    public string   BelegNr             { get; set; } = string.Empty;  // aus NoSerie AENDERUNG

    // Snapshot
    public int      NoteId              { get; set; }
    public int      LehrgangId          { get; set; }
    public string   LehrgangNr          { get; set; } = string.Empty;
    public string   FachBezeichnung     { get; set; } = string.Empty;
    public string   PersonNr            { get; set; } = string.Empty;
    public string   PersonName          { get; set; } = string.Empty;

    public byte?    AlteNote            { get; set; }
    public byte     NeueNote            { get; set; }
    public string?  BewertendeDozentName { get; set; }

    public DateTime Zeitstempel         { get; set; }
    public string   AusfuehrendUser     { get; set; } = string.Empty;
}
