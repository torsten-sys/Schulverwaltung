using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;

namespace Schulverwaltung.Infrastructure.Configurations;

// ============================================================================
//  NoSerie
// ============================================================================
public class NoSerieConfiguration : IEntityTypeConfiguration<NoSerie>
{
    public void Configure(EntityTypeBuilder<NoSerie> b)
    {
        b.ToTable("NoSerie");
        b.HasKey(e => e.NoSerieCode);
        b.Property(e => e.NoSerieCode).HasMaxLength(20).IsRequired();
        b.Property(e => e.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasMany(e => e.Zeilen)
         .WithOne(z => z.NoSerie)
         .HasForeignKey(z => z.NoSerieCode)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NoSerieZeileConfiguration : IEntityTypeConfiguration<NoSerieZeile>
{
    public void Configure(EntityTypeBuilder<NoSerieZeile> b)
    {
        b.ToTable("NoSerieZeile");
        b.HasKey(e => new { e.NoSerieCode, e.StartingNo });
        b.Property(e => e.NoSerieCode).HasMaxLength(20).IsRequired();
        b.Property(e => e.StartingNo).HasMaxLength(20).IsRequired();
        b.Property(e => e.EndingNo).HasMaxLength(20);
        b.Property(e => e.LastNoUsed).HasMaxLength(20);
        b.Property(e => e.Prefix).HasMaxLength(10);
        b.Property(e => e.Suffix).HasMaxLength(10);
        b.Property(e => e.IncrementBy).HasDefaultValue(1);
        b.Property(e => e.NummerLaenge).HasDefaultValue((byte)4);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

// ============================================================================
//  Betrieb
// ============================================================================
public class BetriebConfiguration : IEntityTypeConfiguration<Betrieb>
{
    public void Configure(EntityTypeBuilder<Betrieb> b)
    {
        b.ToTable("Betrieb", t => t.HasTrigger("TR_Betrieb_ModifiedAt"));
        b.HasKey(e => e.BetriebId);
        b.Property(e => e.BetriebId).UseIdentityColumn();
        b.Property(e => e.BetriebNr).HasMaxLength(20).IsRequired();
        b.HasIndex(e => e.BetriebNr).IsUnique();
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.Name2).HasMaxLength(200);
        b.Property(e => e.Strasse).HasMaxLength(200);
        b.Property(e => e.PLZ).HasMaxLength(10);
        b.Property(e => e.Ort).HasMaxLength(100);
        b.Property(e => e.Land).HasMaxLength(50).HasDefaultValue("Deutschland");
        b.Property(e => e.RechStrasse).HasMaxLength(200);
        b.Property(e => e.RechPLZ).HasMaxLength(10);
        b.Property(e => e.RechOrt).HasMaxLength(100);
        b.Property(e => e.RechLand).HasMaxLength(50);
        b.Property(e => e.RechEmail).HasMaxLength(200);
        b.Property(e => e.RechZusatz).HasMaxLength(200);
        b.Property(e => e.Telefon).HasMaxLength(30);
        b.Property(e => e.Email).HasMaxLength(200);
        b.Property(e => e.Website).HasMaxLength(200);
        b.Property(e => e.IstOrthopaedie).HasDefaultValue(false);
        b.Property(e => e.IstPodologie).HasDefaultValue(false);
        b.Property(e => e.IstEmailVerteiler).HasDefaultValue(false);
        b.Property(e => e.IstFoerdermittelberechtigt).HasDefaultValue(false);
        b.Property(e => e.DsgvoCheck).HasDefaultValue(false);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // FK: Ansprechpartner → Person (kein ON DELETE in DB – EF setzt NULL client-seitig)
        b.HasOne(e => e.Ansprechpartner)
         .WithMany()
         .HasForeignKey(e => e.AnsprechpartnerPersonId)
         .OnDelete(DeleteBehavior.ClientSetNull);

        // FK: Ausbilder → Person (kein ON DELETE in DB – EF setzt NULL client-seitig)
        b.HasOne(e => e.Ausbilder)
         .WithMany()
         .HasForeignKey(e => e.AusbilderPersonId)
         .OnDelete(DeleteBehavior.ClientSetNull);
        // PersonRolle → Betrieb Beziehung wird in PersonRolleConfiguration konfiguriert
    }
}

// ============================================================================
//  BetriebAenderungsposten (Snapshot – kein FK zu Betrieb)
// ============================================================================
public class BetriebAenderungspostenConfiguration : IEntityTypeConfiguration<BetriebAenderungsposten>
{
    public void Configure(EntityTypeBuilder<BetriebAenderungsposten> b)
    {
        b.ToTable("BetriebAenderungsposten");
        b.HasKey(e => e.PostenId);
        b.Property(e => e.PostenId).UseIdentityColumn();
        b.Property(e => e.BelegNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.BetriebNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.BetriebName).HasMaxLength(200).IsRequired();
        b.Property(e => e.Ereignis).HasMaxLength(100).IsRequired();
        b.Property(e => e.Tabelle).HasMaxLength(100).IsRequired();
        b.Property(e => e.Feld).HasMaxLength(100);
        b.Property(e => e.AlterWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.NeuerWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.Zeitstempel).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.AusfuehrendUser).HasMaxLength(200).IsRequired();
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        // Kein FK zu Betrieb (Snapshot-Prinzip)
    }
}

// ============================================================================
//  Lehrgang
// ============================================================================
public class LehrgangConfiguration : IEntityTypeConfiguration<Lehrgang>
{
    public void Configure(EntityTypeBuilder<Lehrgang> b)
    {
        b.ToTable("Lehrgang", t => t.HasTrigger("TR_Lehrgang_ModifiedAt"));
        b.HasKey(e => e.LehrgangId);
        b.Property(e => e.LehrgangId).UseIdentityColumn();
        b.Property(e => e.LehrgangNr).HasMaxLength(20).IsRequired();
        b.HasIndex(e => e.LehrgangNr).IsUnique();
        b.Property(e => e.LehrgangTyp)
         .HasConversion<byte>()
         .HasDefaultValue(LehrgangTyp.Meistervorbereitung);
        b.Property(e => e.Bezeichnung).HasMaxLength(200).IsRequired();
        b.Property(e => e.BezeichnungLang).HasMaxLength(500);
        b.Property(e => e.Beschreibung).HasColumnType("nvarchar(max)");
        b.Property(e => e.Status)
         .HasConversion<byte>()
         .HasDefaultValue(LehrgangStatus.Planung);
        b.Property(e => e.MinTeilnehmer).HasDefaultValue(0);
        b.Property(e => e.MaxTeilnehmer).HasDefaultValue(0);
        b.Property(e => e.Gebuehren).HasPrecision(10, 2);
        b.Property(e => e.KostenLehrgang).HasPrecision(10, 2);
        b.Property(e => e.KostenInternatDZ).HasPrecision(10, 2);
        b.Property(e => e.KostenInternatEZ).HasPrecision(10, 2);
        b.Property(e => e.GrundzahlungBetrag).HasPrecision(10, 2);
        b.Property(e => e.KautionWerkstatt).HasPrecision(10, 2);
        b.Property(e => e.KautionInternat).HasPrecision(10, 2);
        b.Property(e => e.Verwaltungspauschale).HasPrecision(10, 2);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedBy).HasMaxLength(100);
        b.Property(e => e.ModifiedBy).HasMaxLength(100);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(e => e.Art)
         .WithMany()
         .HasForeignKey(e => e.ArtId)
         .OnDelete(DeleteBehavior.SetNull);

        b.Ignore(e => e.IstVoll);
        b.Ignore(e => e.FreiePlaetze);
    }
}

// ============================================================================
//  Anhang
// ============================================================================
public class AnhangConfiguration : IEntityTypeConfiguration<Anhang>
{
    public void Configure(EntityTypeBuilder<Anhang> b)
    {
        b.ToTable("Anhang");
        b.HasKey(e => e.AnhangId);
        b.Property(e => e.AnhangId).UseIdentityColumn();
        b.Property(e => e.BezugTyp).HasMaxLength(50).IsRequired();
        b.Property(e => e.Bezeichnung).HasMaxLength(200).IsRequired();
        b.Property(e => e.DateiName).HasMaxLength(200).IsRequired();
        b.Property(e => e.DateiTyp).HasMaxLength(50).IsRequired();
        b.Property(e => e.HochgeladenVon).HasMaxLength(100);
        b.Property(e => e.HochgeladenAm).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.Inhalt).HasColumnType("varbinary(max)");
    }
}

// ============================================================================
//  LehrgangPerson
// ============================================================================
public class LehrgangPersonConfiguration : IEntityTypeConfiguration<LehrgangPerson>
{
    public void Configure(EntityTypeBuilder<LehrgangPerson> b)
    {
        b.ToTable("LehrgangPerson");
        // Composite PK: gleiche Person kann als Teilnehmer UND Dozent in einem Lehrgang sein
        b.HasKey(e => new { e.LehrgangId, e.PersonId, e.Rolle });

        b.Property(e => e.Rolle).HasConversion<byte>();
        b.Property(e => e.Status).HasDefaultValue((byte)1);
        b.Property(e => e.AnmeldungsDatum).HasDefaultValueSql("CAST(SYSUTCDATETIME() AS DATE)");
        b.Property(e => e.GeplanteStunden).HasColumnType("decimal(6,2)");
        b.Property(e => e.BetriebName).HasMaxLength(200);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(e => e.Lehrgang)
         .WithMany(l => l.Personen)
         .HasForeignKey(e => e.LehrgangId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.Person)
         .WithMany()
         .HasForeignKey(e => e.PersonId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}

// ============================================================================
//  EinladungsVorlage
// ============================================================================
public class EinladungsVorlageConfiguration : IEntityTypeConfiguration<EinladungsVorlage>
{
    public void Configure(EntityTypeBuilder<EinladungsVorlage> b)
    {
        b.ToTable("EinladungsVorlage");
        b.HasKey(e => e.EinladungsVorlageId);
        b.Property(e => e.Anschreiben).HasColumnType("nvarchar(max)");
        b.Property(e => e.ZahlungsplanText).HasColumnType("nvarchar(max)");
        b.Property(e => e.InternatAbschnitt).HasColumnType("nvarchar(max)");
        b.Property(e => e.RatenplanText).HasColumnType("nvarchar(max)");
        b.Property(e => e.Schlusstext).HasColumnType("nvarchar(max)");
    }
}

// ============================================================================
//  LehrgangEinladung
// ============================================================================
public class LehrgangEinladungConfiguration : IEntityTypeConfiguration<LehrgangEinladung>
{
    public void Configure(EntityTypeBuilder<LehrgangEinladung> b)
    {
        b.ToTable("LehrgangEinladung");
        b.HasKey(e => e.LehrgangEinladungId);
        b.Property(e => e.LehrgangEinladungId).UseIdentityColumn();
        b.Property(e => e.Status).HasDefaultValue((byte)0);
        b.Property(e => e.ErstelltAm).HasDefaultValueSql("GETDATE()");
        b.HasIndex(e => new { e.LehrgangId, e.PersonId }).IsUnique();

        b.HasOne(e => e.Lehrgang)
         .WithMany()
         .HasForeignKey(e => e.LehrgangId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
