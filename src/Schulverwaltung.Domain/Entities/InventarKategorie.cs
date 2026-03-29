using Schulverwaltung.Domain.Common;
namespace Schulverwaltung.Domain.Entities;

public class InventarKategorie : AuditableEntity
{
    public int    KategorieId { get; set; }
    public string Code        { get; set; } = string.Empty;
    public string Bezeichnung { get; set; } = string.Empty;
    public int    Reihenfolge { get; set; } = 0;
    public bool   Gesperrt    { get; set; } = false;
    public ICollection<Inventar> Inventare { get; set; } = new List<Inventar>();
}
