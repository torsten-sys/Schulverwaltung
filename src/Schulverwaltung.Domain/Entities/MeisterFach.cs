using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Prüfungsfach innerhalb eines Meisterkurs-Lehrgangs.
/// </summary>
public class MeisterFach : AuditableEntity
{
    public int     FachId      { get; set; }
    public int     LehrgangId  { get; set; }
    public string  Bezeichnung { get; set; } = string.Empty;

    /// <summary>Gewichtung für Gesamtnote-Berechnung. Precision 5,2.</summary>
    public decimal Gewichtung  { get; set; } = 1.0m;
    public int     Reihenfolge { get; set; } = 0;

    // Navigation
    public Lehrgang            Lehrgang { get; set; } = null!;
    public ICollection<MeisterNote> Noten { get; set; } = new List<MeisterNote>();
}
