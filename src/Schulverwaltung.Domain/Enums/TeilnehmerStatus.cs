namespace Schulverwaltung.Domain.Enums;

public enum TeilnehmerStatus : byte
{
    Warteliste      = 0,
    Angemeldet      = 1,
    Abgemeldet      = 2,
    Bestanden       = 3,
    NichtBestanden  = 4
}
