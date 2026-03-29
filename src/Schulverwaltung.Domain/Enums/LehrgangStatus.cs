namespace Schulverwaltung.Domain.Enums;

public enum LehrgangStatus : byte
{
    Planung         = 0,
    AnmeldungOffen  = 1,
    Aktiv           = 2,
    Abgeschlossen   = 3,
    Storniert       = 4
}
