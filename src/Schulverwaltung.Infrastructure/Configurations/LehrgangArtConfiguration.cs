using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schulverwaltung.Domain.Entities;

namespace Schulverwaltung.Infrastructure.Configurations;

public class LehrgangArtConfiguration : IEntityTypeConfiguration<LehrgangArt>
{
    public void Configure(EntityTypeBuilder<LehrgangArt> b)
    {
        b.ToTable("LehrgangArt", t => t.HasTrigger("TR_LehrgangArt_ModifiedAt"));
        b.HasKey(e => e.ArtId);
        b.Property(e => e.ArtId).UseIdentityColumn();
        b.Property(e => e.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UQ_LehrgangArt_Code");
        b.Property(e => e.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(e => e.Reihenfolge).HasDefaultValue(0);
        b.Property(e => e.Gesperrt).HasDefaultValue(false);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

public class LehrgangEinheitConfiguration : IEntityTypeConfiguration<LehrgangEinheit>
{
    public void Configure(EntityTypeBuilder<LehrgangEinheit> b)
    {
        b.ToTable("LehrgangEinheit", t =>
        {
            t.HasTrigger("TR_LehrgangEinheit_ModifiedAt");
            t.HasCheckConstraint("CK_LehrgangEinheit_Typ", "[EinheitTyp] IN (0,1,2,3)");
        });
        b.HasKey(e => e.EinheitId);
        b.Property(e => e.EinheitId).UseIdentityColumn();
        b.Property(e => e.Thema).HasMaxLength(200).IsRequired();
        b.Property(e => e.Inhalt).HasColumnType("nvarchar(max)");
        b.Property(e => e.RaumBezeichnung).HasMaxLength(100);
        b.Property(e => e.EinheitTyp).HasDefaultValue((byte)0);
        b.Property(e => e.Reihenfolge).HasDefaultValue(0);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(e => new { e.LehrgangId, e.Datum, e.Reihenfolge });

        b.HasOne(e => e.Lehrgang)
         .WithMany(l => l.Einheiten)
         .HasForeignKey(e => e.LehrgangId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.Dozent)
         .WithMany()
         .HasForeignKey(e => e.DozentPersonId)
         .OnDelete(DeleteBehavior.SetNull);
    }
}

public class LehrgangAenderungspostenConfiguration : IEntityTypeConfiguration<LehrgangAenderungsposten>
{
    public void Configure(EntityTypeBuilder<LehrgangAenderungsposten> b)
    {
        b.ToTable("LehrgangAenderungsposten");
        b.HasKey(e => e.PostenId);
        b.Property(e => e.PostenId).UseIdentityColumn();
        b.Property(e => e.BelegNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.LehrgangNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.LehrgangBezeichnung).HasMaxLength(200).IsRequired();
        b.Property(e => e.Ereignis).HasMaxLength(50).IsRequired();
        b.Property(e => e.Tabelle).HasMaxLength(100);
        b.Property(e => e.Feld).HasMaxLength(100);
        b.Property(e => e.AlterWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.NeuerWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.Zeitstempel).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.AusfuehrendUser).HasMaxLength(100).IsRequired();
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        // Kein FK zu Lehrgang (Snapshot-Prinzip)
    }
}
