using Schulverwaltung.Domain.Common;
namespace Schulverwaltung.Domain.Entities;

public class RaumTyp : AuditableEntity
{
    public int    RaumTypId   { get; set; }
    public string Code        { get; set; } = string.Empty;
    public string Bezeichnung { get; set; } = string.Empty;
    public int    Reihenfolge { get; set; } = 0;
    public bool   IstInternat { get; set; } = false;
    public bool   Gesperrt    { get; set; } = false;
    public ICollection<Raum> Raeume { get; set; } = new List<Raum>();
}
