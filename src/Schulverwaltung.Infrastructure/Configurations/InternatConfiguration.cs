using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schulverwaltung.Domain.Entities;

namespace Schulverwaltung.Infrastructure.Configurations;

// ============================================================================
//  InternatBelegung
// ============================================================================
public class InternatBelegungConfiguration : IEntityTypeConfiguration<InternatBelegung>
{
    public void Configure(EntityTypeBuilder<InternatBelegung> b)
    {
        b.ToTable("InternatBelegung", t =>
        {
            t.HasTrigger("TR_InternatBelegung_ModifiedAt");
            t.HasCheckConstraint("CK_InternatBelegung_Datum",    "[Von] <= [Bis]");
            t.HasCheckConstraint("CK_InternatBelegung_Typ",      "[BelegungsTyp] IN (0,1,2)");
            t.HasCheckConstraint("CK_InternatBelegung_KostenArt","[KostenArt] IN (0,1,2)");
        });
        b.HasKey(e => e.BelegungId);
        b.Property(e => e.BelegungId).UseIdentityColumn();
        b.Property(e => e.PersonNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.PersonName).HasMaxLength(200).IsRequired();
        b.Property(e => e.LehrgangNr).HasMaxLength(20);
        b.Property(e => e.LehrgangBezeichnung).HasMaxLength(200);
        b.Property(e => e.BelegungsTyp).HasDefaultValue((byte)0);
        b.Property(e => e.KostenArt).HasDefaultValue((byte)1);
        b.Property(e => e.Kosten).HasPrecision(10, 2);
        b.Property(e => e.Bezahlt).HasDefaultValue(false);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedBy).HasMaxLength(100);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(e => new { e.RaumId, e.Von, e.Bis })
         .HasDatabaseName("IX_InternatBelegung_RaumZeitraum");

        // FK Raum: Restrict (kein Löschen wenn belegt)
        b.HasOne(e => e.Raum)
         .WithMany(r => r.Belegungen)
         .HasForeignKey(e => e.RaumId)
         .OnDelete(DeleteBehavior.Restrict);

        // FK Lehrgang: SetNull (Lehrgang kann enden)
        b.HasOne(e => e.Lehrgang)
         .WithMany()
         .HasForeignKey(e => e.LehrgangId)
         .OnDelete(DeleteBehavior.SetNull);
    }
}

// ============================================================================
//  InternatAenderungsposten
// ============================================================================
public class InternatAenderungspostenConfiguration : IEntityTypeConfiguration<InternatAenderungsposten>
{
    public void Configure(EntityTypeBuilder<InternatAenderungsposten> b)
    {
        b.ToTable("InternatAenderungsposten", t =>
        {
            t.HasTrigger("TR_InternatAenderungsposten_Protect");
        });
        b.HasKey(e => e.PostenId);
        b.Property(e => e.PostenId).UseIdentityColumn();
        b.Property(e => e.BelegNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.ZimmerNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.ZimmerBezeichnung).HasMaxLength(100).IsRequired();
        b.Property(e => e.PersonNr).HasMaxLength(20);
        b.Property(e => e.PersonName).HasMaxLength(200);
        b.Property(e => e.Ereignis).HasMaxLength(50).IsRequired();
        b.Property(e => e.Tabelle).HasMaxLength(100);
        b.Property(e => e.Feld).HasMaxLength(100);
        b.Property(e => e.AlterWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.NeuerWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.Zeitstempel).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.AusfuehrendUser).HasMaxLength(100).IsRequired();
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");

        // Kein FK – reines Snapshot-Prinzip
    }
}
