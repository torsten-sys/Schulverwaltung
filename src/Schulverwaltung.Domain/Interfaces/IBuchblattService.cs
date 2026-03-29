using Schulverwaltung.Domain.Entities;

namespace Schulverwaltung.Domain.Interfaces;

// ---------------------------------------------------------------------------
//  PersonRolle-Service-Interface (analog BC Codeunit)
// ---------------------------------------------------------------------------

public interface IPersonRolleService
{
    /// <summary>Rolle einer Person zuweisen (und Änderungsposten schreiben).</summary>
    Task RolleZuweisenAsync(
        int personId, PersonRolleTyp rolle, int? betriebId, string user,
        CancellationToken ct = default);

    /// <summary>Aktive Rolle einer Person entfernen (und Änderungsposten schreiben).</summary>
    Task RolleEntfernenAsync(
        int personId, PersonRolleTyp rolle, string user,
        CancellationToken ct = default);

    /// <summary>Prüft ob eine Person eine aktive Rolle hat.</summary>
    Task<bool> HatAktiveRolleAsync(
        int personId, PersonRolleTyp rolle,
        CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
//  Validierungsergebnis (allgemein verwendbar)
// ---------------------------------------------------------------------------
public record BuchblattValidierungErgebnis(
    bool                        Erfolgreich,
    IReadOnlyList<string>       Fehler,
    IReadOnlyList<string>       Warnungen
)
{
    public static BuchblattValidierungErgebnis Ok(IReadOnlyList<string>? warnungen = null) =>
        new(true, Array.Empty<string>(), warnungen ?? Array.Empty<string>());

    public static BuchblattValidierungErgebnis MitFehler(params string[] messages) =>
        new(false, messages, Array.Empty<string>());
}
