using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Infrastructure.Configurations;

namespace Schulverwaltung.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext für die Schulverwaltung.
/// Konfiguration ausgelagert in IEntityTypeConfiguration-Klassen (Fluent API).
/// </summary>
public class SchulverwaltungDbContext : DbContext
{
    public SchulverwaltungDbContext(DbContextOptions<SchulverwaltungDbContext> options)
        : base(options) { }

    // ── Infrastruktur ────────────────────────────────────────────────────────
    public DbSet<NoSerie>      NoSerien      => Set<NoSerie>();
    public DbSet<NoSerieZeile> NoSerieZeilen => Set<NoSerieZeile>();

    // ── Benutzerverwaltung ───────────────────────────────────────────────────
    public DbSet<AppBenutzer>                   AppBenutzer                   => Set<AppBenutzer>();
    public DbSet<AppBenutzerAenderungsposten>   AppBenutzerAenderungsposten   => Set<AppBenutzerAenderungsposten>();

    // ── Stammdaten ───────────────────────────────────────────────────────────
    public DbSet<Betrieb>                       Betriebe                       => Set<Betrieb>();
    public DbSet<BetriebAenderungsposten>       BetriebAenderungsposten        => Set<BetriebAenderungsposten>();
    public DbSet<Organisation>                  Organisationen                 => Set<Organisation>();
    public DbSet<OrganisationAenderungsposten>  OrganisationAenderungsposten   => Set<OrganisationAenderungsposten>();
    public DbSet<LehrgangArt>                   LehrgangArten                  => Set<LehrgangArt>();
    public DbSet<Lehrgang>                      Lehrgaenge                     => Set<Lehrgang>();
    public DbSet<LehrgangEinheit>               LehrgangEinheiten              => Set<LehrgangEinheit>();
    public DbSet<LehrgangAenderungsposten>      LehrgangAenderungsposten       => Set<LehrgangAenderungsposten>();

    // ── Personen & Rollen ────────────────────────────────────────────────────
    public DbSet<Person>               Personen               => Set<Person>();
    public DbSet<PersonRolle>          PersonRollen           => Set<PersonRolle>();
    public DbSet<PersonDozentProfil>    PersonDozentProfile    => Set<PersonDozentProfil>();
    public DbSet<PersonPatientProfil>   PersonPatientProfile   => Set<PersonPatientProfil>();
    public DbSet<PersonAenderungsposten> PersonAenderungsposten => Set<PersonAenderungsposten>();

    // ── Anhänge ───────────────────────────────────────────────────────────────
    public DbSet<Anhang> Anhaenge => Set<Anhang>();

    // ── Lehrgang-Verknüpfungen ────────────────────────────────────────────────
    public DbSet<LehrgangPerson> LehrgangPersonen => Set<LehrgangPerson>();

    // ── Internat ──────────────────────────────────────────────────────────────
    public DbSet<InternatBelegung>           InternatBelegungen         => Set<InternatBelegung>();
    public DbSet<InternatAenderungsposten>   InternatAenderungsposten   => Set<InternatAenderungsposten>();

    // ── Räume & Inventar ─────────────────────────────────────────────────────
    public DbSet<RaumTyp>                  RaumTypen                => Set<RaumTyp>();
    public DbSet<Raum>                     Raeume                   => Set<Raum>();
    public DbSet<InventarKategorie>        InventarKategorien       => Set<InventarKategorie>();
    public DbSet<Inventar>                 Inventar                 => Set<Inventar>();
    public DbSet<InventarAenderungsposten> InventarAenderungsposten => Set<InventarAenderungsposten>();
    public DbSet<InventarKomponente>       InventarKomponenten      => Set<InventarKomponente>();
    public DbSet<Organisationseinheit>     Organisationseinheiten   => Set<Organisationseinheit>();
    public DbSet<InventarWartung>          InventarWartungen        => Set<InventarWartung>();

    // ── Dokumente ────────────────────────────────────────────────────────────
    public DbSet<Briefvorlage> Briefvorlagen => Set<Briefvorlage>();
    public DbSet<EinladungsVorlage>  EinladungsVorlagen  => Set<EinladungsVorlage>();
    public DbSet<LehrgangEinladung>  LehrgangEinladungen => Set<LehrgangEinladung>();

    // ── Meisterkurs ───────────────────────────────────────────────────────────
    public DbSet<MeisterAbschnitt>              MeisterAbschnitte             => Set<MeisterAbschnitt>();
    public DbSet<MeisterFach>                   MeisterFaecher                => Set<MeisterFach>();
    public DbSet<MeisterNote>                   MeisterNoten                  => Set<MeisterNote>();
    public DbSet<MeisterNoteAenderungsposten>   MeisterNoteAenderungsposten   => Set<MeisterNoteAenderungsposten>();
    public DbSet<MeisterFunktion>               MeisterFunktionen             => Set<MeisterFunktion>();
    public DbSet<MeisterPatientenZuordnung>     MeisterPatientenZuordnungen   => Set<MeisterPatientenZuordnung>();
    public DbSet<MeisterPatientenTermin>        MeisterPatientenTermine       => Set<MeisterPatientenTermin>();
    public DbSet<MeisterPatientenBuchungsposten> MeisterPatientenBuchungsposten => Set<MeisterPatientenBuchungsposten>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Alle IEntityTypeConfiguration<T> aus diesem Assembly automatisch laden
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchulverwaltungDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Automatisches Setzen von CreatedAt / ModifiedAt vor jedem Speichern.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is Domain.Common.AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt  = now;
                    auditable.ModifiedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.ModifiedAt = now;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
