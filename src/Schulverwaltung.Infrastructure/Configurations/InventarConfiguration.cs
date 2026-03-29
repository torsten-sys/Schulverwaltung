using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schulverwaltung.Domain.Entities;

namespace Schulverwaltung.Infrastructure.Configurations;

// ============================================================================
//  RaumTyp
// ============================================================================
public class RaumTypConfiguration : IEntityTypeConfiguration<RaumTyp>
{
    public void Configure(EntityTypeBuilder<RaumTyp> b)
    {
        b.ToTable("RaumTyp", t =>
        {
            t.HasTrigger("TR_RaumTyp_ModifiedAt");
        });

        b.HasKey(e => e.RaumTypId);
        b.Property(e => e.RaumTypId).UseIdentityColumn();
        b.Property(e => e.Code).HasMaxLength(20).IsRequired();
        b.Property(e => e.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(e => e.Reihenfolge).HasDefaultValue(0);
        b.Property(e => e.IstInternat).HasDefaultValue(false);
        b.Property(e => e.Gesperrt).HasDefaultValue(false);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(e => e.Code).IsUnique()
         .HasDatabaseName("UQ_RaumTyp_Code");

        b.HasMany(e => e.Raeume)
         .WithOne(r => r.RaumTyp)
         .HasForeignKey(r => r.RaumTypId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}

// ============================================================================
//  Raum
// ============================================================================
public class RaumConfiguration : IEntityTypeConfiguration<Raum>
{
    public void Configure(EntityTypeBuilder<Raum> b)
    {
        b.ToTable("Raum", t =>
        {
            t.HasTrigger("TR_Raum_ModifiedAt");
        });

        b.HasKey(e => e.RaumId);
        b.Property(e => e.RaumId).UseIdentityColumn();
        b.Property(e => e.RaumNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(e => e.Kapazitaet);
        b.Property(e => e.SperrGrund).HasMaxLength(200);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.Gesperrt).HasDefaultValue(false);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(e => e.RaumTyp)
         .WithMany(rt => rt.Raeume)
         .HasForeignKey(e => e.RaumTypId)
         .OnDelete(DeleteBehavior.Restrict)
         .HasConstraintName("FK_Raum_RaumTyp");
    }
}

// ============================================================================
//  InventarKategorie
// ============================================================================
public class InventarKategorieConfiguration : IEntityTypeConfiguration<InventarKategorie>
{
    public void Configure(EntityTypeBuilder<InventarKategorie> b)
    {
        b.ToTable("InventarKategorie", t =>
        {
            t.HasTrigger("TR_InventarKategorie_ModifiedAt");
        });

        b.HasKey(e => e.KategorieId);
        b.Property(e => e.KategorieId).UseIdentityColumn();
        b.Property(e => e.Code).HasMaxLength(20).IsRequired();
        b.Property(e => e.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(e => e.Reihenfolge).HasDefaultValue(0);
        b.Property(e => e.Gesperrt).HasDefaultValue(false);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(e => e.Code).IsUnique()
         .HasDatabaseName("UQ_InventarKategorie_Code");
    }
}

// ============================================================================
//  Inventar
// ============================================================================
public class InventarConfiguration : IEntityTypeConfiguration<Inventar>
{
    public void Configure(EntityTypeBuilder<Inventar> b)
    {
        b.ToTable("Inventar", t =>
        {
            t.HasTrigger("TR_Inventar_ModifiedAt");
            t.HasCheckConstraint("CK_Inventar_Zustand", "[Zustand] BETWEEN 0 AND 3");
        });

        b.HasKey(e => e.InventarId);
        b.Property(e => e.InventarId).UseIdentityColumn();
        b.Property(e => e.InventarNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.Bezeichnung).HasMaxLength(200).IsRequired();
        b.Property(e => e.Typ).HasMaxLength(100);
        b.Property(e => e.Seriennummer).HasMaxLength(100);
        b.Property(e => e.Anschaffungskosten).HasPrecision(10, 2);
        b.Property(e => e.SperrGrund).HasMaxLength(200);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.Zustand).HasDefaultValue((byte)0);
        b.Property(e => e.Gesperrt).HasDefaultValue(false);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(e => e.InventarNr).IsUnique()
         .HasDatabaseName("UQ_Inventar_Nr");

        b.HasOne(e => e.Kategorie)
         .WithMany(k => k.Inventare)
         .HasForeignKey(e => e.KategorieId)
         .OnDelete(DeleteBehavior.Restrict)
         .HasConstraintName("FK_Inventar_Kategorie");

        b.HasOne(e => e.Raum)
         .WithMany(r => r.Inventare)
         .HasForeignKey(e => e.RaumId)
         .OnDelete(DeleteBehavior.SetNull)
         .HasConstraintName("FK_Inventar_Raum");

        b.HasOne(e => e.Person)
         .WithMany()
         .HasForeignKey(e => e.PersonId)
         .OnDelete(DeleteBehavior.SetNull)
         .HasConstraintName("FK_Inventar_Person");

        b.HasOne(e => e.OrgEinheit)
         .WithMany(o => o.Inventare)
         .HasForeignKey(e => e.OrgEinheitId)
         .OnDelete(DeleteBehavior.SetNull)
         .HasConstraintName("FK_Inventar_OrgEinheit");

        b.HasMany(e => e.Komponenten)
         .WithOne(k => k.Inventar)
         .HasForeignKey(k => k.InventarId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(e => e.Wartungen)
         .WithOne(w => w.Inventar)
         .HasForeignKey(w => w.InventarId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

// ============================================================================
//  InventarAenderungsposten
// ============================================================================
public class InventarAenderungspostenConfiguration : IEntityTypeConfiguration<InventarAenderungsposten>
{
    public void Configure(EntityTypeBuilder<InventarAenderungsposten> b)
    {
        b.ToTable("InventarAenderungsposten", t =>
        {
            t.HasTrigger("TR_InventarAenderungsposten_Protect");
        });

        b.HasKey(e => e.PostenId);
        b.Property(e => e.PostenId).UseIdentityColumn();
        b.Property(e => e.BelegNr).HasMaxLength(20);
        b.Property(e => e.InventarNr).HasMaxLength(20);
        b.Property(e => e.Bezeichnung).HasMaxLength(200);
        b.Property(e => e.Ereignis).HasMaxLength(50);
        b.Property(e => e.Feld).HasMaxLength(100);
        b.Property(e => e.AlterWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.NeuerWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.AusfuehrendUser).HasMaxLength(100);
        b.Property(e => e.Zeitstempel).HasDefaultValueSql("SYSUTCDATETIME()");

        // Kein FK – Snapshot-Prinzip
    }
}

// ============================================================================
//  Organisationseinheit
// ============================================================================
public class OrganisationseinheitConfiguration : IEntityTypeConfiguration<Organisationseinheit>
{
    public void Configure(EntityTypeBuilder<Organisationseinheit> b)
    {
        b.ToTable("Organisationseinheit", t => t.HasTrigger("TR_Organisationseinheit_ModifiedAt"));
        b.HasKey(e => e.OrgEinheitId);
        b.Property(e => e.OrgEinheitId).UseIdentityColumn();
        b.Property(e => e.Code).HasMaxLength(20).IsRequired();
        b.Property(e => e.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(e => e.Reihenfolge).HasDefaultValue(0);
        b.Property(e => e.Gesperrt).HasDefaultValue(false);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UQ_Organisationseinheit_Code");
    }
}

// ============================================================================
//  InventarKomponente
// ============================================================================
public class InventarKomponenteConfiguration : IEntityTypeConfiguration<InventarKomponente>
{
    public void Configure(EntityTypeBuilder<InventarKomponente> b)
    {
        b.ToTable("InventarKomponente");
        b.HasKey(e => e.KomponenteId);
        b.Property(e => e.KomponenteId).UseIdentityColumn();
        b.Property(e => e.Bezeichnung).HasMaxLength(200).IsRequired();
        b.Property(e => e.Menge).HasPrecision(10, 2).HasDefaultValue(1m);
        b.Property(e => e.Seriennummer).HasMaxLength(100);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.Reihenfolge).HasDefaultValue(0);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasOne(e => e.Inventar)
         .WithMany(i => i.Komponenten)
         .HasForeignKey(e => e.InventarId)
         .OnDelete(DeleteBehavior.Cascade)
         .HasConstraintName("FK_InventarKomponente_Inventar");
    }
}

// ============================================================================
//  InventarWartung
// ============================================================================
public class InventarWartungConfiguration : IEntityTypeConfiguration<InventarWartung>
{
    public void Configure(EntityTypeBuilder<InventarWartung> b)
    {
        b.ToTable("InventarWartung", t => t.HasTrigger("TR_InventarWartung_Protect"));
        b.HasKey(e => e.WartungId);
        b.Property(e => e.WartungId).UseIdentityColumn();
        b.Property(e => e.BetriebName).HasMaxLength(200);
        b.Property(e => e.Anmerkungen).HasColumnType("nvarchar(max)");
        b.Property(e => e.AusfuehrendUser).HasMaxLength(100).IsRequired();
        b.Property(e => e.ErstelltAm).HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasOne(e => e.Inventar)
         .WithMany(i => i.Wartungen)
         .HasForeignKey(e => e.InventarId)
         .OnDelete(DeleteBehavior.Cascade)
         .HasConstraintName("FK_InventarWartung_Inventar");
        b.HasOne(e => e.Betrieb)
         .WithMany()
         .HasForeignKey(e => e.BetriebId)
         .OnDelete(DeleteBehavior.SetNull)
         .HasConstraintName("FK_InventarWartung_Betrieb");
    }
}
