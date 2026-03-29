namespace Schulverwaltung.Domain.Common;

/// <summary>
/// Basisklasse für alle Stammdaten-Entitäten.
/// Enthält Audit-Felder analog zu BC (SystemCreatedAt, SystemModifiedAt).
/// </summary>
public abstract class AuditableEntity
{
    public DateTime CreatedAt  { get; set; }
    public DateTime ModifiedAt { get; set; }
}
