using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Termin innerhalb einer Patientenzuordnung.
/// Pro Zuordnung max. 1 Termin pro TerminTyp (Unique-Index).
/// </summary>
public class MeisterPatientenTermin : AuditableEntity
{
    public int      TerminId     { get; set; }
    public int      ZuordnungId  { get; set; }

    /// <summary>0=Vorstellung, 1=Zwischenprobe, 2=Auslieferung</summary>
    public byte     TerminTyp    { get; set; } = 0;

    public DateOnly? Datum        { get; set; }
    public TimeOnly? Uhrzeit      { get; set; }

    /// <summary>0=Geplant, 1=Bestaetigt, 2=Durchgefuehrt, 3=Ausgefallen</summary>
    public byte     Status       { get; set; } = 0;

    /// <summary>Nur relevant bei TerminTyp=2 (Auslieferung)</summary>
    public bool?    HilfsmittelUebergeben  { get; set; }
    public string?  NichtUebergebenGrund   { get; set; }
    public string?  Notiz                  { get; set; }

    // Navigation
    public MeisterPatientenZuordnung Zuordnung { get; set; } = null!;
}
