using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Briefkopf/Fuß-Vorlage für Dokumente (Berichte, Briefe, etc.).
/// </summary>
public class Briefvorlage : AuditableEntity
{
    public int     BriefvorlageId { get; set; }
    public string  Bezeichnung    { get; set; } = string.Empty;
    public string? KopfHtml       { get; set; }
    public string? FussHtml       { get; set; }
    public bool    IstStandard    { get; set; }
    public bool    Gesperrt       { get; set; }
    public string? Notiz          { get; set; }
}
