namespace Schulverwaltung.Domain.Entities;

public class InventarKomponente
{
    public int      KomponenteId { get; set; }
    public int      InventarId   { get; set; }
    public string   Bezeichnung  { get; set; } = string.Empty; // max 200
    public decimal  Menge        { get; set; } = 1m; // precision 10,2
    public string?  Seriennummer { get; set; } // max 100 - Label: "ID / Seriennummer"
    public string?  Notiz        { get; set; }
    public int      Reihenfolge  { get; set; } = 0;
    public DateTime CreatedAt    { get; set; }
    // Navigation
    public Inventar Inventar { get; set; } = null!;
}
