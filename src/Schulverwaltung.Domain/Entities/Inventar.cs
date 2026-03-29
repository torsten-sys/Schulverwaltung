using Schulverwaltung.Domain.Common;
namespace Schulverwaltung.Domain.Entities;

public class Inventar : AuditableEntity
{
    public int       InventarId             { get; set; }
    public string    InventarNr             { get; set; } = string.Empty;
    public string    Bezeichnung            { get; set; } = string.Empty;
    public string?   Typ                    { get; set; } // max 100, nullable
    public int       KategorieId            { get; set; }
    public string?   Seriennummer           { get; set; }
    public DateOnly? Anschaffungsdatum      { get; set; }
    public decimal?  Anschaffungskosten     { get; set; }
    public int?      RaumId                 { get; set; }
    public int?      PersonId               { get; set; }
    public int?      OrgEinheitId           { get; set; } // nullable FK
    public byte      Zustand                { get; set; } = 0; // 0=Gut, 1=Beschaedigt, 2=Defekt, 3=Ausgemustert
    public DateOnly? WartungStartdatum      { get; set; }
    public int?      WartungIntervallMonate { get; set; }
    public DateOnly? WartungLetztesDatum    { get; set; }
    public DateOnly? WartungNaechstesDatum  { get; set; }
    public bool      Gesperrt               { get; set; } = false;
    public string?   SperrGrund             { get; set; }
    public string?   Notiz                  { get; set; }
    public InventarKategorie         Kategorie   { get; set; } = null!;
    public Raum?                     Raum        { get; set; }
    public Person?                   Person      { get; set; }
    public Organisationseinheit?     OrgEinheit  { get; set; }
    public ICollection<InventarKomponente> Komponenten { get; set; } = new List<InventarKomponente>();
    public ICollection<InventarWartung>    Wartungen   { get; set; } = new List<InventarWartung>();
}
