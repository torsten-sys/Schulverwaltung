using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schulverwaltung.Domain.Entities;

namespace Schulverwaltung.Infrastructure.Configurations;

// ============================================================================
//  Organisation
// ============================================================================
public class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> b)
    {
        b.ToTable("Organisation", t =>
        {
            t.HasTrigger("TR_Organisation_ModifiedAt");
            t.HasCheckConstraint("CK_Organisation_Typ", "[OrganisationsTyp] IN (0,1)");
        });

        b.HasKey(e => e.OrganisationId);
        b.Property(e => e.OrganisationId).UseIdentityColumn();
        b.Property(e => e.OrganisationsNr).HasMaxLength(20).IsRequired();
        b.HasIndex(e => e.OrganisationsNr).IsUnique();
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.Kurzbezeichnung).HasMaxLength(20);
        b.Property(e => e.Strasse).HasMaxLength(100);
        b.Property(e => e.PLZ).HasMaxLength(10);
        b.Property(e => e.Ort).HasMaxLength(100);
        b.Property(e => e.Land).HasMaxLength(100).HasDefaultValue("Deutschland");
        b.Property(e => e.Telefon).HasMaxLength(50);
        b.Property(e => e.Email).HasMaxLength(200);
        b.Property(e => e.Website).HasMaxLength(200);
        b.Property(e => e.VereinbarteUebernachtungskosten).HasPrecision(10, 2);
        b.Property(e => e.Sammelrechnung).HasDefaultValue(false);
        b.Property(e => e.Gesperrt).HasDefaultValue(false);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // Betriebe via InnungsId
        b.HasMany(e => e.Betriebe)
         .WithOne(b2 => b2.Innung)
         .HasForeignKey(b2 => b2.InnungsId)
         .OnDelete(DeleteBehavior.SetNull);

        // Betriebe via HandwerkskammerId
        b.HasMany(e => e.Kammerbetriebe)
         .WithOne(b2 => b2.Handwerkskammer)
         .HasForeignKey(b2 => b2.HandwerkskammerId)
         .OnDelete(DeleteBehavior.SetNull);
    }
}

// ============================================================================
//  OrganisationAenderungsposten (Snapshot – kein FK zu Organisation)
// ============================================================================
public class OrganisationAenderungspostenConfiguration : IEntityTypeConfiguration<OrganisationAenderungsposten>
{
    public void Configure(EntityTypeBuilder<OrganisationAenderungsposten> b)
    {
        b.ToTable("OrganisationAenderungsposten");
        b.HasKey(e => e.PostenId);
        b.Property(e => e.PostenId).UseIdentityColumn();
        b.Property(e => e.BelegNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.OrganisationsNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.OrganisationsName).HasMaxLength(200).IsRequired();
        b.Property(e => e.Ereignis).HasMaxLength(50).IsRequired();
        b.Property(e => e.Tabelle).HasMaxLength(100);
        b.Property(e => e.Feld).HasMaxLength(100);
        b.Property(e => e.AlterWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.NeuerWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.Zeitstempel).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.AusfuehrendUser).HasMaxLength(100).IsRequired();
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        // Kein FK zu Organisation (Snapshot-Prinzip)
    }
}
