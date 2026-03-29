using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schulverwaltung.Domain.Entities;

namespace Schulverwaltung.Infrastructure.Configurations;

public class AppBenutzerConfiguration : IEntityTypeConfiguration<AppBenutzer>
{
    public void Configure(EntityTypeBuilder<AppBenutzer> b)
    {
        b.ToTable("AppBenutzer", t =>
        {
            t.HasTrigger("TR_AppBenutzer_ModifiedAt");
            t.HasCheckConstraint("CK_AppBenutzer_Rolle", "[AppRolle] BETWEEN 0 AND 3");
        });

        b.HasKey(e => e.BenutzerId);
        b.Property(e => e.BenutzerId).UseIdentityColumn();
        b.Property(e => e.AdBenutzername).HasMaxLength(100).IsRequired();
        b.Property(e => e.DisplayName).HasMaxLength(200);
        b.Property(e => e.Email).HasMaxLength(200);
        b.Property(e => e.AppRolle).HasDefaultValue((byte)0);
        b.Property(e => e.Gesperrt).HasDefaultValue(false);
        b.Property(e => e.SperrGrund).HasMaxLength(200);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.DarfInventarVerwalten).HasDefaultValue(false);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(e => e.AdBenutzername).IsUnique()
         .HasDatabaseName("UQ_AppBenutzer_ADName");

        b.HasOne(e => e.Person)
         .WithMany()
         .HasForeignKey(e => e.PersonId)
         .OnDelete(DeleteBehavior.SetNull)
         .HasConstraintName("FK_AppBenutzer_Person");
    }
}

public class AppBenutzerAenderungspostenConfiguration : IEntityTypeConfiguration<AppBenutzerAenderungsposten>
{
    public void Configure(EntityTypeBuilder<AppBenutzerAenderungsposten> b)
    {
        b.ToTable("AppBenutzerAenderungsposten", t =>
        {
            t.HasTrigger("TR_AppBenutzerAenderungsposten_Protect");
        });

        b.HasKey(e => e.PostenId);
        b.Property(e => e.PostenId).UseIdentityColumn();
        b.Property(e => e.BelegNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.AdBenutzername).HasMaxLength(100).IsRequired();
        b.Property(e => e.DisplayName).HasMaxLength(200);
        b.Property(e => e.Ereignis).HasMaxLength(50).IsRequired();
        b.Property(e => e.Tabelle).HasMaxLength(100);
        b.Property(e => e.Feld).HasMaxLength(100);
        b.Property(e => e.AlterWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.NeuerWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.AusfuehrendUser).HasMaxLength(100).IsRequired();
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.Zeitstempel).HasDefaultValueSql("SYSUTCDATETIME()");

        // Kein FK – Snapshot-Prinzip
    }
}
