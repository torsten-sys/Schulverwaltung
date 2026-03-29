using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Einzelne Einheit (Termin) eines Lehrgangs – Ablaufplan.
/// EinheitTyp: 0=Unterricht, 1=Prüfung, 2=Pause, 3=Exkursion
/// </summary>
public class LehrgangEinheit : AuditableEntity
{
    public int      EinheitId       { get; set; }
    public int      LehrgangId      { get; set; }
    public DateOnly Datum           { get; set; }
    public TimeOnly? UhrzeitVon    { get; set; }
    public TimeOnly? UhrzeitBis    { get; set; }
    public string   Thema           { get; set; } = string.Empty;
    public string?  Inhalt          { get; set; }
    public int?     DozentPersonId  { get; set; }
    public string?  RaumBezeichnung { get; set; }
    public byte     EinheitTyp      { get; set; } = 0;
    public int      Reihenfolge     { get; set; } = 0;
    public string?  Notiz           { get; set; }

    // Navigation
    public Lehrgang Lehrgang { get; set; } = null!;
    public Person?  Dozent   { get; set; }
}
