namespace Schulverwaltung.Domain.Entities;

/// <summary>Singleton-Einladungsvorlage (immer ID=1).</summary>
public class EinladungsVorlage
{
    public int     EinladungsVorlageId { get; set; } = 1;
    public string? Anschreiben         { get; set; }
    public string? ZahlungsplanText    { get; set; }
    public string? InternatAbschnitt   { get; set; }
    public string? RatenplanText       { get; set; }
    public string? Schlusstext         { get; set; }
}
