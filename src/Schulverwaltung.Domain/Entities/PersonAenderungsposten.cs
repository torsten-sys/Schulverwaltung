namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Unveränderlicher Änderungsposten für Personen (Snapshot-Prinzip).
/// Kein UPDATE/DELETE erlaubt – via DB-Trigger geschützt.
/// </summary>
public class PersonAenderungsposten
{
    public int             PostenId        { get; set; }  // PK, IDENTITY
    public string          BelegNr         { get; set; } = string.Empty;  // gruppiert zusammengehörige Änderungen
    public int             PersonId        { get; set; }  // kein FK – Snapshot
    public string          PersonNr        { get; set; } = string.Empty;
    public string          PersonName      { get; set; } = string.Empty;
    public string          Ereignis        { get; set; } = string.Empty;  // "RolleZugewiesen"/"StammdatenGeaendert"/…
    public string          Tabelle         { get; set; } = string.Empty;
    public string?         Feld            { get; set; }
    public string?         AlterWert       { get; set; }
    public string?         NeuerWert       { get; set; }
    public PersonRolleTyp? RolleTyp        { get; set; }
    public DateTime        Zeitstempel     { get; set; }
    public string          AusfuehrendUser { get; set; } = string.Empty;
    public string?         Notiz           { get; set; }
}
