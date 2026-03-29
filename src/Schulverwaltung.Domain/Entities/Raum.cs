using Schulverwaltung.Domain.Common;
namespace Schulverwaltung.Domain.Entities;

public class Raum : AuditableEntity
{
    public int     RaumId      { get; set; }
    public string  RaumNr      { get; set; } = string.Empty;
    public string  Bezeichnung { get; set; } = string.Empty;
    public int     RaumTypId   { get; set; }
    public int?    Kapazitaet  { get; set; }
    public bool    Gesperrt    { get; set; } = false;
    public string? SperrGrund  { get; set; }
    public string? Notiz       { get; set; }
    public RaumTyp RaumTyp     { get; set; } = null!;
    public ICollection<Inventar> Inventare { get; set; } = new List<Inventar>();
    public ICollection<InternatBelegung> Belegungen { get; set; } = new List<InternatBelegung>();
}
