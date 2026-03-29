namespace Schulverwaltung.Domain.Entities;

public class LehrgangEinladung
{
    public int      LehrgangEinladungId { get; set; }
    public int      LehrgangId          { get; set; }
    public int      PersonId            { get; set; }
    public DateTime ErstelltAm          { get; set; } = DateTime.UtcNow;
    public DateTime? GesendetAm         { get; set; }
    public byte     Status              { get; set; } = 0; // 0=Entwurf, 1=Fertig

    public Lehrgang Lehrgang { get; set; } = null!;
}
