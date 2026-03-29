using Schulverwaltung.Domain.Entities;

namespace Schulverwaltung.Domain.Interfaces;

// ---------------------------------------------------------------------------
//  Generisches Repository-Interface (analog BC Table-Objekt)
// ---------------------------------------------------------------------------
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
}

// ---------------------------------------------------------------------------
//  Spezialisierte Repository-Interfaces
// ---------------------------------------------------------------------------

public interface ILehrgangRepository : IRepository<Lehrgang>
{
    Task<Lehrgang?> GetByNrAsync(string lehrgangNr, CancellationToken ct = default);

    /// <summary>Lehrgang mit allen Personen laden (für Karte / FastTabs).</summary>
    Task<Lehrgang?> GetWithDetailsAsync(int lehrgangId, CancellationToken ct = default);

    /// <summary>Lehrgänge die sich mit einem Zeitraum überschneiden (Parallelprüfung).</summary>
    Task<IReadOnlyList<Lehrgang>> GetUeberschneidendeAsync(
        DateOnly start, DateOnly? ende, int? ausschliesseLehrgangId = null,
        CancellationToken ct = default);
}

public interface IPersonRepository : IRepository<Person>
{
    Task<Person?> GetByNrAsync(string personNr, CancellationToken ct = default);
    Task<IReadOnlyList<Person>> SucheAsync(string suchtext, CancellationToken ct = default);

    /// <summary>Alle Personen mit einer bestimmten Rolle laden.</summary>
    Task<IReadOnlyList<Person>> GetByRolleAsync(PersonRolleTyp rolle, CancellationToken ct = default);

    /// <summary>Person mit allen Rollen laden.</summary>
    Task<Person?> GetWithRollenAsync(int personId, CancellationToken ct = default);
}

public interface IBetriebRepository : IRepository<Betrieb>
{
    Task<Betrieb?> GetByNrAsync(string betriebNr, CancellationToken ct = default);
    Task<IReadOnlyList<Betrieb>> SucheAsync(string suchtext, CancellationToken ct = default);
}

public interface INoSerieRepository
{
    /// <summary>Nächste Nummer aus einer Serie ziehen (threadsicher, mit DB-Sperre).</summary>
    Task<string> GetNextNoAsync(string noSerieCode, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
//  Unit of Work (analog BC COMMIT)
// ---------------------------------------------------------------------------
public interface IUnitOfWork : IAsyncDisposable
{
    ILehrgangRepository  Lehrgaenge { get; }
    IPersonRepository    Personen   { get; }
    IBetriebRepository   Betriebe   { get; }
    INoSerieRepository   NoSerien   { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
