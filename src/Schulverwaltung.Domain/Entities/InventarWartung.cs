namespace Schulverwaltung.Domain.Entities;

public class InventarWartung
{
    public int      WartungId       { get; set; }
    public int      InventarId      { get; set; }
    public DateOnly WartungsDatum   { get; set; }
    public bool     IstExtern       { get; set; } = false;
    public int?     BetriebId       { get; set; } // nullable FK auf Betrieb
    public string?  BetriebName     { get; set; } // Snapshot, max 200
    public string?  Anmerkungen     { get; set; }
    public string   AusfuehrendUser { get; set; } = string.Empty; // max 100
    public DateTime ErstelltAm      { get; set; }
    // Navigation
    public Inventar  Inventar { get; set; } = null!;
    public Betrieb?  Betrieb  { get; set; }
}
