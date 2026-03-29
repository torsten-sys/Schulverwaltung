using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Interfaces;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Infrastructure.Repositories;

// ============================================================================
//  Generisches Basis-Repository
// ============================================================================
public abstract class BaseRepository<T> : IRepository<T> where T : class
{
    protected readonly SchulverwaltungDbContext _db;
    protected BaseRepository(SchulverwaltungDbContext db) => _db = db;

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Set<T>().FindAsync(new object[] { id }, ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await _db.Set<T>().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await _db.Set<T>().AddAsync(entity, ct);

    public void Update(T entity) => _db.Set<T>().Update(entity);
    public void Delete(T entity) => _db.Set<T>().Remove(entity);
}

// ============================================================================
//  LehrgangRepository
// ============================================================================
public class LehrgangRepository : BaseRepository<Lehrgang>, ILehrgangRepository
{
    public LehrgangRepository(SchulverwaltungDbContext db) : base(db) { }

    public async Task<Lehrgang?> GetByNrAsync(string nr, CancellationToken ct = default)
        => await _db.Lehrgaenge.FirstOrDefaultAsync(l => l.LehrgangNr == nr, ct);

    public async Task<Lehrgang?> GetWithDetailsAsync(int id, CancellationToken ct = default)
        => await _db.Lehrgaenge
            .Include(l => l.Personen)
                .ThenInclude(lp => lp.Person)
            .FirstOrDefaultAsync(l => l.LehrgangId == id, ct);

    public async Task<IReadOnlyList<Lehrgang>> GetUeberschneidendeAsync(
        DateOnly start, DateOnly? ende, int? ausschliessen = null, CancellationToken ct = default)
    {
        var q = _db.Lehrgaenge.Where(l =>
            l.Status != Domain.Enums.LehrgangStatus.Storniert &&
            l.StartDatum <= (ende ?? start) &&
            (l.EndDatum == null || l.EndDatum >= start));

        if (ausschliessen.HasValue)
            q = q.Where(l => l.LehrgangId != ausschliessen.Value);

        return await q.ToListAsync(ct);
    }
}

// ============================================================================
//  PersonRepository
// ============================================================================
public class PersonRepository : BaseRepository<Person>, IPersonRepository
{
    public PersonRepository(SchulverwaltungDbContext db) : base(db) { }

    public async Task<Person?> GetByNrAsync(string nr, CancellationToken ct = default)
        => await _db.Personen.FirstOrDefaultAsync(p => p.PersonNr == nr, ct);

    public async Task<IReadOnlyList<Person>> SucheAsync(string s, CancellationToken ct = default)
        => await _db.Personen
            .Where(p => p.Nachname.Contains(s) || p.Vorname.Contains(s) ||
                        (p.Email != null && p.Email.Contains(s)))
            .OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Person>> GetByRolleAsync(PersonRolleTyp rolle, CancellationToken ct = default)
        => await _db.PersonRollen
            .Where(r => r.RolleTyp == rolle && r.Status == 0)
            .Include(r => r.Person)
            .Select(r => r.Person)
            .OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .ToListAsync(ct);

    public async Task<Person?> GetWithRollenAsync(int personId, CancellationToken ct = default)
        => await _db.Personen
            .Include(p => p.Rollen)
                .ThenInclude(r => r.Betrieb)
            .FirstOrDefaultAsync(p => p.PersonId == personId, ct);
}

// ============================================================================
//  BetriebRepository
// ============================================================================
public class BetriebRepository : BaseRepository<Betrieb>, IBetriebRepository
{
    public BetriebRepository(SchulverwaltungDbContext db) : base(db) { }

    public async Task<Betrieb?> GetByNrAsync(string nr, CancellationToken ct = default)
        => await _db.Betriebe.FirstOrDefaultAsync(b => b.BetriebNr == nr, ct);

    public async Task<IReadOnlyList<Betrieb>> SucheAsync(string s, CancellationToken ct = default)
        => await _db.Betriebe
            .Where(b => b.Name.Contains(s) || (b.Name2 != null && b.Name2.Contains(s)) ||
                        (b.Ort != null && b.Ort.Contains(s)))
            .OrderBy(b => b.Name)
            .ToListAsync(ct);
}

// ============================================================================
//  NoSerieRepository
// ============================================================================
public class NoSerieRepository : INoSerieRepository
{
    private readonly SchulverwaltungDbContext _db;
    public NoSerieRepository(SchulverwaltungDbContext db) => _db = db;

    public async Task<string> GetNextNoAsync(string noSerieCode, CancellationToken ct = default)
    {
        var param = new Microsoft.Data.SqlClient.SqlParameter
        {
            ParameterName = "@NextNo",
            SqlDbType     = System.Data.SqlDbType.NVarChar,
            Size          = 20,
            Direction     = System.Data.ParameterDirection.Output
        };

        await _db.Database.ExecuteSqlRawAsync(
            "EXEC sp_GetNextNo @NoSerieCode = {0}, @NextNo = @NextNo OUTPUT",
            new object[] { noSerieCode, param },
            ct);

        return param.Value?.ToString()
            ?? throw new InvalidOperationException($"Keine Nummer für Serie '{noSerieCode}' erhalten.");
    }
}

// ============================================================================
//  Unit of Work
// ============================================================================
public class UnitOfWork : IUnitOfWork
{
    private readonly SchulverwaltungDbContext _db;

    public ILehrgangRepository  Lehrgaenge { get; }
    public IPersonRepository    Personen   { get; }
    public IBetriebRepository   Betriebe   { get; }
    public INoSerieRepository   NoSerien   { get; }

    public UnitOfWork(SchulverwaltungDbContext db)
    {
        _db        = db;
        Lehrgaenge = new LehrgangRepository(db);
        Personen   = new PersonRepository(db);
        Betriebe   = new BetriebRepository(db);
        NoSerien   = new NoSerieRepository(db);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public async ValueTask DisposeAsync()
        => await _db.DisposeAsync();
}
