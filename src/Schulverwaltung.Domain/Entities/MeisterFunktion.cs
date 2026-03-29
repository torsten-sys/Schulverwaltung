namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Funktionsträger-Zuweisung innerhalb eines Meisterkurs-Lehrgangs.
/// PersonId/-Nr/-Name sind Snapshots (kein FK auf Person).
/// GueltigBis = null bedeutet aktuell aktiv.
/// </summary>
public class MeisterFunktion
{
    public int       FunktionId  { get; set; }
    public int       LehrgangId  { get; set; }

    // Snapshot – kein FK auf Person
    public int       PersonId    { get; set; }
    public string    PersonNr    { get; set; } = string.Empty;
    public string    PersonName  { get; set; } = string.Empty;

    /// <summary>
    /// 0=Gruppensprecher, 1=Klassenbuchwart, 2=Postwart, 3=Lagerwart,
    /// 4=Bandsaegenraumwart, 5=Kunststoffwerkstattwart, 6=MassUndGipsraumwart,
    /// 7=Schaftraumwart, 8=Werkstattwart, 9=Internatswart
    /// </summary>
    public byte      Funktion    { get; set; } = 0;
    public DateOnly  GueltigAb   { get; set; }
    public DateOnly? GueltigBis  { get; set; }  // null = aktiv

    public string?   Notiz       { get; set; }
    public string?   CreatedBy   { get; set; }
    public DateTime  CreatedAt   { get; set; }

    // Navigation
    public Lehrgang Lehrgang { get; set; } = null!;
}
