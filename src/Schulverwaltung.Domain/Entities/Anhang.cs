namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Dateianhang zu einem beliebigen Bezugsobjekt (Person, Lehrgang, Betrieb, …).
/// BezugTyp + BezugId identifizieren das Elternobjekt (kein FK – polymorph).
/// </summary>
public class Anhang
{
    public int      AnhangId       { get; set; }  // PK, IDENTITY
    public string   BezugTyp       { get; set; } = string.Empty;  // 'Person'/'Lehrgang'/'Betrieb'
    public int      BezugId        { get; set; }
    public string   Bezeichnung    { get; set; } = string.Empty;
    public string   DateiName      { get; set; } = string.Empty;
    public string   DateiTyp       { get; set; } = string.Empty;
    public int      DateiGroesse   { get; set; }
    public byte[]   Inhalt         { get; set; } = Array.Empty<byte>();
    public string?  HochgeladenVon { get; set; }
    public DateTime HochgeladenAm  { get; set; } = DateTime.UtcNow;
}
