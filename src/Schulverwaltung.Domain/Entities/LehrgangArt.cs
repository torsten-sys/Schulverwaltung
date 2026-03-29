using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Lehrgangsart – frei konfigurierbare Klassifizierung (z.B. Intern, Extern, Gefördert).
/// </summary>
public class LehrgangArt : AuditableEntity
{
    public int     ArtId       { get; set; }
    public string  Code        { get; set; } = string.Empty;  // max 20, unique
    public string  Bezeichnung { get; set; } = string.Empty;
    public int     Reihenfolge { get; set; } = 0;
    public bool    Gesperrt    { get; set; } = false;
    public string? Notiz       { get; set; }
}
