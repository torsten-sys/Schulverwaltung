using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Zuordnung eines Patienten zu einem Meisterkurs-Abschnitt (PV oder Prüfung).
/// Alle Personen-Felder sind Snapshots (kein FK auf Person).
/// </summary>
public class MeisterPatientenZuordnung : AuditableEntity
{
    public int    ZuordnungId   { get; set; }
    public int    LehrgangId    { get; set; }
    public int    AbschnittId   { get; set; }

    // Patient – Snapshot
    public int    PatientPersonId  { get; set; }
    public string PatientPersonNr  { get; set; } = string.Empty;
    public string PatientName      { get; set; } = string.Empty;

    // Meisterschüler 1 – Snapshot
    public int    Meisterschueler1PersonId { get; set; }
    public string Meisterschueler1Nr       { get; set; } = string.Empty;
    public string Meisterschueler1Name     { get; set; } = string.Empty;

    // Meisterschüler 2 – Snapshot (optional)
    public int?   Meisterschueler2PersonId { get; set; }
    public string? Meisterschueler2Nr      { get; set; }
    public string? Meisterschueler2Name    { get; set; }

    public bool   IstErsatzpatient              { get; set; } = false;

    /// <summary>null=ausstehend, true=zugelassen, false=abgelehnt</summary>
    public bool?  PruefungskommissionZugelassen { get; set; }

    /// <summary>0=Angefragt, 1=Zugesagt, 2=Eingeteilt, 3=Absage, 4=Ersatz</summary>
    public byte   ZuordnungsStatus { get; set; } = 0;

    /// <summary>0=Planung, 1=Bestaetigt, 2=Gebucht</summary>
    public byte   BuchungsStatus   { get; set; } = 0;

    public string? Notiz    { get; set; }
    public string? CreatedBy { get; set; }

    // Navigation
    public MeisterAbschnitt Abschnitt { get; set; } = null!;
    public ICollection<MeisterPatientenTermin> Termine { get; set; } = new List<MeisterPatientenTermin>();
}
