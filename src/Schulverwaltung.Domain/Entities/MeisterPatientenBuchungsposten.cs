namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Unveränderlicher Buchungsposten für eine abgeschlossene Patientenversorgung.
/// Kein FK (Snapshot-Prinzip). Trigger-geschützt gegen UPDATE/DELETE.
/// BelegNr aus NoSerie "MEISTERBUCHUNG" (Format MB-000001).
/// </summary>
public class MeisterPatientenBuchungsposten
{
    public int     PostenId     { get; set; }
    public string  BelegNr      { get; set; } = string.Empty;

    // Lehrgang-Snapshot
    public int     LehrgangId   { get; set; }
    public string  LehrgangNr   { get; set; } = string.Empty;

    // Abschnitt-Snapshot
    public int     AbschnittNummer      { get; set; }
    public string  AbschnittBezeichnung { get; set; } = string.Empty;

    public DateTime BuchungsDatum { get; set; }

    // Patient-Snapshot
    public int     PatientPersonId { get; set; }
    public string  PatientNr       { get; set; } = string.Empty;
    public string  PatientName     { get; set; } = string.Empty;

    // Meisterschüler 1-Snapshot
    public int     Meisterschueler1PersonId { get; set; }
    public string  MS1Nr                    { get; set; } = string.Empty;
    public string  MS1Name                  { get; set; } = string.Empty;

    // Meisterschüler 2-Snapshot
    public int?    Meisterschueler2PersonId { get; set; }
    public string? MS2Nr                    { get; set; }
    public string? MS2Name                  { get; set; }

    public bool    IstErsatzpatient             { get; set; }
    public bool?   PruefungskommissionZugelassen { get; set; }

    // Termin-Snapshots
    public DateOnly? Termin1Datum   { get; set; }
    public byte?     Termin1Status  { get; set; }
    public DateOnly? Termin2Datum   { get; set; }
    public byte?     Termin2Status  { get; set; }
    public DateOnly? Termin3Datum   { get; set; }
    public byte?     Termin3Status  { get; set; }

    public bool?   HilfsmittelUebergeben  { get; set; }
    public string? NichtUebergebenGrund   { get; set; }

    public string  GebuchtvonUser { get; set; } = string.Empty;
    public DateTime GebuchtAm     { get; set; }
}
