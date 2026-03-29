using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Note eines Meisterschülers in einem Prüfungsfach.
/// PersonId ist ein Snapshot (kein FK).
/// </summary>
public class MeisterNote : AuditableEntity
{
    public int      NoteId      { get; set; }
    public int      LehrgangId  { get; set; }
    public int      FachId      { get; set; }

    // Snapshot – kein FK
    public int      PersonId    { get; set; }
    public string   PersonNr    { get; set; } = string.Empty;
    public string   PersonName  { get; set; } = string.Empty;

    /// <summary>Note 1–6. null = noch nicht bewertet.</summary>
    public byte?    Note        { get; set; }

    // Bewertender Dozent – Snapshot (kein FK)
    public int?     BewertendeDozentPersonId { get; set; }
    public string?  BewertendeDozentName     { get; set; }
    public DateOnly? BewertungsDatum         { get; set; }

    public string?  Notiz     { get; set; }
    public string?  CreatedBy { get; set; }

    // Navigation (FK auf MeisterFach)
    public MeisterFach Fach { get; set; } = null!;
}
