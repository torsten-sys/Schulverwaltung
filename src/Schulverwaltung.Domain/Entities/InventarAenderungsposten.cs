using Schulverwaltung.Domain.Common;
namespace Schulverwaltung.Domain.Entities;

public class InventarAenderungsposten : AuditableEntity
{
    public int     PostenId        { get; set; }
    public string  BelegNr         { get; set; } = string.Empty;
    public int     InventarId      { get; set; }  // Snapshot – kein FK
    public string  InventarNr      { get; set; } = string.Empty;
    public string  Bezeichnung     { get; set; } = string.Empty;
    public string  Ereignis        { get; set; } = string.Empty;
    public string? Feld            { get; set; }
    public string? AlterWert       { get; set; }
    public string? NeuerWert       { get; set; }
    public DateTime Zeitstempel    { get; set; }
    public string  AusfuehrendUser { get; set; } = string.Empty;
}
