using Schulverwaltung.Domain.Common;
namespace Schulverwaltung.Domain.Entities;

public class Organisationseinheit : AuditableEntity
{
    public int    OrgEinheitId { get; set; }
    public string Code        { get; set; } = string.Empty; // max 20, unique
    public string Bezeichnung { get; set; } = string.Empty; // max 100
    public int    Reihenfolge { get; set; } = 0;
    public bool   Gesperrt    { get; set; } = false;
    public ICollection<Inventar> Inventare { get; set; } = new List<Inventar>();
}
