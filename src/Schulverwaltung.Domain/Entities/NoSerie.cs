using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Nummernserie – Kopftabelle (analog BC "No. Series").
/// Definiert eine benannte Serie, z.B. "LEHRGANG" oder "TEILNEHMER".
/// </summary>
public class NoSerie : AuditableEntity
{
    public string  NoSerieCode      { get; set; } = string.Empty;
    public string  Bezeichnung      { get; set; } = string.Empty;
    public bool    Standardmaessig  { get; set; } = true;
    public bool    Datumsgeb        { get; set; } = false;  // Jahreszahl in Nummer einbauen
    public string? Notiz            { get; set; }

    // Navigation
    public ICollection<NoSerieZeile> Zeilen { get; set; } = new List<NoSerieZeile>();
}

/// <summary>
/// Nummernserie – Zeilentabelle (analog BC "No. Series Line").
/// Definiert einen Nummernbereich innerhalb einer Serie.
/// </summary>
public class NoSerieZeile : AuditableEntity
{
    public string  NoSerieCode    { get; set; } = string.Empty;
    public string  StartingNo     { get; set; } = string.Empty;
    public string? EndingNo       { get; set; }
    public string? LastNoUsed     { get; set; }
    public DateOnly? LastDateUsed { get; set; }
    public int     IncrementBy    { get; set; } = 1;
    public bool    AllowGaps      { get; set; } = false;
    public bool    Offen          { get; set; } = true;
    public string? Prefix         { get; set; }
    public string? Suffix         { get; set; }
    public byte    NummerLaenge   { get; set; } = 4;

    // Navigation
    public NoSerie NoSerie { get; set; } = null!;
}
