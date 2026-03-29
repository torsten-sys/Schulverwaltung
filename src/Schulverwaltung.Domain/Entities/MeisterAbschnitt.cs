using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Meisterkurs-Abschnitt – einer von 10 Prüfungsabschnitten eines Meisterkurs-Lehrgangs.
/// </summary>
public class MeisterAbschnitt : AuditableEntity
{
    public int    AbschnittId  { get; set; }
    public int    LehrgangId   { get; set; }
    public int    Nummer       { get; set; }   // 1–10
    public string Bezeichnung  { get; set; } = string.Empty;

    /// <summary>0=Vorbereitung, 1=Patientenversorgung, 2=Meisterpruefung</summary>
    public byte   AbschnittTyp { get; set; } = 0;
    public string? Beschreibung { get; set; }
    public int    Reihenfolge  { get; set; }

    /// <summary>0=Geplant, 1=Aktiv, 2=Abgeschlossen</summary>
    public byte   Status       { get; set; } = 0;

    // Navigation
    public Lehrgang Lehrgang { get; set; } = null!;
    public ICollection<MeisterPatientenZuordnung> Zuordnungen { get; set; } = new List<MeisterPatientenZuordnung>();
}
