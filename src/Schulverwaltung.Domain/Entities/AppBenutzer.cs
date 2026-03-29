using Schulverwaltung.Domain.Common;

namespace Schulverwaltung.Domain.Entities;

/// <summary>
/// Repräsentiert einen Windows-AD-Benutzer, der sich an der Anwendung angemeldet hat.
/// Kein eigenes Passwort – Authentifizierung via Windows Authentication (Negotiate).
/// </summary>
public class AppBenutzer : AuditableEntity
{
    public int     BenutzerId      { get; set; }

    /// <summary>Windows-AD-Name, z.B. "DOMAIN\username" oder "username@domain.de"</summary>
    public string  AdBenutzername  { get; set; } = string.Empty;

    /// <summary>Anzeigename aus Active Directory (beim ersten Login befüllt)</summary>
    public string? DisplayName     { get; set; }

    /// <summary>E-Mail-Adresse aus Active Directory</summary>
    public string? Email           { get; set; }

    /// <summary>
    /// Anwendungsrolle:
    /// 0 = Gast        (Standard bei erstem Login – nur Listen lesen)
    /// 1 = Sachbearbeiter (Stammdaten + Lehrgänge pflegen)
    /// 2 = Dozent      (+ Noten erfassen)
    /// 3 = Administrator (voller Zugriff + Benutzerverwaltung)
    /// </summary>
    public byte    AppRolle        { get; set; } = 0;

    /// <summary>Verknüpfung zur Person-Entität (besonders relevant für Dozenten)</summary>
    public int?    PersonId        { get; set; }

    public bool    Gesperrt        { get; set; } = false;
    public string? SperrGrund      { get; set; }

    public DateTime? ErsterLogin   { get; set; }
    public DateTime? LetzterLogin  { get; set; }

    public string?   Notiz         { get; set; }

    public bool    DarfInventarVerwalten { get; set; } = false;

    // Navigation
    public Person? Person { get; set; }
}
