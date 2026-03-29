using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

public class InternatBelegung : AuditableEntity
{
    public int      BelegungId          { get; set; }
    public int?     RaumId              { get; set; }
    public int      PersonId            { get; set; }         // Snapshot – kein FK
    public string   PersonNr            { get; set; } = string.Empty;
    public string   PersonName          { get; set; } = string.Empty;
    public int?     LehrgangId          { get; set; }         // FK auf Lehrgang (nullable)
    public string?  LehrgangNr          { get; set; }
    public string?  LehrgangBezeichnung { get; set; }
    public byte     BelegungsTyp        { get; set; } = 0;   // 0=Meisterkurs, 1=Sonderlehrgang, 2=Dozent
    public DateOnly Von                 { get; set; }
    public DateOnly Bis                 { get; set; }
    public byte     KostenArt           { get; set; } = 1;   // 0=Pauschale, 1=Individuell, 2=Unentgeltlich
    public decimal? Kosten              { get; set; }
    public bool     Bezahlt             { get; set; } = false;
    public DateOnly? BezahltAm         { get; set; }
    public string?  Notiz               { get; set; }
    public string?  CreatedBy           { get; set; }

    public Raum?           Raum    { get; set; }
    public Lehrgang?       Lehrgang { get; set; }
}
