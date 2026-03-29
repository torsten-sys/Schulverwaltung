using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schulverwaltung.Domain.Entities;

namespace Schulverwaltung.Infrastructure.Configurations;

// ============================================================================
//  MeisterAbschnitt
// ============================================================================
public class MeisterAbschnittConfiguration : IEntityTypeConfiguration<MeisterAbschnitt>
{
    public void Configure(EntityTypeBuilder<MeisterAbschnitt> b)
    {
        b.ToTable("MeisterAbschnitt", t =>
        {
            t.HasTrigger("TR_MeisterAbschnitt_ModifiedAt");
            t.HasCheckConstraint("CK_MeisterAbschnitt_Nummer",      "[Nummer] BETWEEN 1 AND 10");
            t.HasCheckConstraint("CK_MeisterAbschnitt_AbschnittTyp","[AbschnittTyp] IN (0,1,2)");
        });
        b.HasKey(e => e.AbschnittId);
        b.Property(e => e.AbschnittId).UseIdentityColumn();
        b.Property(e => e.Bezeichnung).HasMaxLength(200).IsRequired();
        b.Property(e => e.Beschreibung).HasColumnType("nvarchar(max)");
        b.Property(e => e.AbschnittTyp).HasDefaultValue((byte)0);
        b.Property(e => e.Status).HasDefaultValue((byte)0);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(e => new { e.LehrgangId, e.Nummer }).IsUnique()
         .HasDatabaseName("UQ_MeisterAbschnitt_LehrgangNummer");

        b.HasOne(e => e.Lehrgang)
         .WithMany(l => l.MeisterAbschnitte)
         .HasForeignKey(e => e.LehrgangId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

// ============================================================================
//  MeisterFach
// ============================================================================
public class MeisterFachConfiguration : IEntityTypeConfiguration<MeisterFach>
{
    public void Configure(EntityTypeBuilder<MeisterFach> b)
    {
        b.ToTable("MeisterFach", t => t.HasTrigger("TR_MeisterFach_ModifiedAt"));
        b.HasKey(e => e.FachId);
        b.Property(e => e.FachId).UseIdentityColumn();
        b.Property(e => e.Bezeichnung).HasMaxLength(200).IsRequired();
        b.Property(e => e.Gewichtung).HasPrecision(5, 2).HasDefaultValue(1.0m);
        b.Property(e => e.Reihenfolge).HasDefaultValue(0);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(e => new { e.LehrgangId, e.Bezeichnung }).IsUnique()
         .HasDatabaseName("UQ_MeisterFach_LehrgangBezeichnung");

        b.HasOne(e => e.Lehrgang)
         .WithMany(l => l.MeisterFaecher)
         .HasForeignKey(e => e.LehrgangId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

// ============================================================================
//  MeisterNote
// ============================================================================
public class MeisterNoteConfiguration : IEntityTypeConfiguration<MeisterNote>
{
    public void Configure(EntityTypeBuilder<MeisterNote> b)
    {
        b.ToTable("MeisterNote", t =>
        {
            t.HasTrigger("TR_MeisterNote_ModifiedAt");
            t.HasCheckConstraint("CK_MeisterNote_Note", "[Note] BETWEEN 1 AND 6");
        });
        b.HasKey(e => e.NoteId);
        b.Property(e => e.NoteId).UseIdentityColumn();
        b.Property(e => e.PersonNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.PersonName).HasMaxLength(200).IsRequired();
        b.Property(e => e.BewertendeDozentName).HasMaxLength(200);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedBy).HasMaxLength(100);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(e => new { e.LehrgangId, e.FachId, e.PersonId }).IsUnique()
         .HasDatabaseName("UQ_MeisterNote_Lehrgang_Fach_Person");

        // FK zu LehrgangFach (Cascade)
        b.HasOne(e => e.Fach)
         .WithMany(f => f.Noten)
         .HasForeignKey(e => e.FachId)
         .OnDelete(DeleteBehavior.Cascade);

        // FK zu Lehrgang (kein Navigation auf Lehrgang-Seite nötig)
        b.HasOne<Domain.Entities.Lehrgang>()
         .WithMany()
         .HasForeignKey(e => e.LehrgangId)
         .OnDelete(DeleteBehavior.NoAction); // Cascade kommt via Fach
    }
}

// ============================================================================
//  MeisterNoteAenderungsposten (Snapshot – Trigger-geschützt)
// ============================================================================
public class MeisterNoteAenderungspostenConfiguration : IEntityTypeConfiguration<MeisterNoteAenderungsposten>
{
    public void Configure(EntityTypeBuilder<MeisterNoteAenderungsposten> b)
    {
        b.ToTable("MeisterNoteAenderungsposten", t => t.HasTrigger("TR_MeisterNoteAenderungsposten_Protect"));
        b.HasKey(e => e.PostenId);
        b.Property(e => e.PostenId).UseIdentityColumn();
        b.Property(e => e.BelegNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.LehrgangNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.FachBezeichnung).HasMaxLength(200).IsRequired();
        b.Property(e => e.PersonNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.PersonName).HasMaxLength(200).IsRequired();
        b.Property(e => e.BewertendeDozentName).HasMaxLength(200);
        b.Property(e => e.Zeitstempel).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.AusfuehrendUser).HasMaxLength(100).IsRequired();
        // Kein FK (Snapshot-Prinzip)
    }
}

// ============================================================================
//  MeisterFunktion
// ============================================================================
public class MeisterFunktionConfiguration : IEntityTypeConfiguration<MeisterFunktion>
{
    public void Configure(EntityTypeBuilder<MeisterFunktion> b)
    {
        b.ToTable("MeisterFunktion", t =>
            t.HasCheckConstraint("CK_MeisterFunktion_Funktion", "[Funktion] BETWEEN 0 AND 9"));
        b.HasKey(e => e.FunktionId);
        b.Property(e => e.FunktionId).UseIdentityColumn();
        b.Property(e => e.PersonNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.PersonName).HasMaxLength(200).IsRequired();
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedBy).HasMaxLength(100);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // Gefilterter Index: schneller Zugriff auf aktive Funktionsträger
        b.HasIndex(e => new { e.LehrgangId, e.Funktion })
         .HasFilter("[GueltigBis] IS NULL")
         .HasDatabaseName("IX_MeisterFunktion_Aktiv");

        b.HasOne(e => e.Lehrgang)
         .WithMany(l => l.MeisterFunktionen)
         .HasForeignKey(e => e.LehrgangId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

// ============================================================================
//  MeisterPatientenZuordnung
// ============================================================================
public class MeisterPatientenZuordnungConfiguration : IEntityTypeConfiguration<MeisterPatientenZuordnung>
{
    public void Configure(EntityTypeBuilder<MeisterPatientenZuordnung> b)
    {
        b.ToTable("MeisterPatientenZuordnung", t =>
        {
            t.HasTrigger("TR_MeisterPatientenZuordnung_ModifiedAt");
            t.HasCheckConstraint("CK_MeisterPZ_BuchungsStatus",    "[BuchungsStatus] IN (0,1,2)");
            t.HasCheckConstraint("CK_MeisterPZ_ZuordnungsStatus",  "[ZuordnungsStatus] IN (0,1,2,3,4)");
        });
        b.HasKey(e => e.ZuordnungId);
        b.Property(e => e.ZuordnungId).UseIdentityColumn();
        b.Property(e => e.PatientPersonNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.PatientName).HasMaxLength(200).IsRequired();
        b.Property(e => e.Meisterschueler1Nr).HasMaxLength(20).IsRequired();
        b.Property(e => e.Meisterschueler1Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.Meisterschueler2Nr).HasMaxLength(20);
        b.Property(e => e.Meisterschueler2Name).HasMaxLength(200);
        b.Property(e => e.IstErsatzpatient).HasDefaultValue(false);
        b.Property(e => e.ZuordnungsStatus).HasDefaultValue((byte)0);
        b.Property(e => e.BuchungsStatus).HasDefaultValue((byte)0);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedBy).HasMaxLength(100);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(e => e.Abschnitt)
         .WithMany(a => a.Zuordnungen)
         .HasForeignKey(e => e.AbschnittId)
         .OnDelete(DeleteBehavior.Cascade);

        // FK zu Lehrgang (kein Navigation auf Lehrgang-Seite nötig, Cascade via Abschnitt)
        b.HasOne<Domain.Entities.Lehrgang>()
         .WithMany()
         .HasForeignKey(e => e.LehrgangId)
         .OnDelete(DeleteBehavior.NoAction);
    }
}

// ============================================================================
//  MeisterPatientenTermin
// ============================================================================
public class MeisterPatientenTerminConfiguration : IEntityTypeConfiguration<MeisterPatientenTermin>
{
    public void Configure(EntityTypeBuilder<MeisterPatientenTermin> b)
    {
        b.ToTable("MeisterPatientenTermin", t =>
        {
            t.HasTrigger("TR_MeisterPatientenTermin_ModifiedAt");
            t.HasCheckConstraint("CK_MeisterTermin_Typ",    "[TerminTyp] IN (0,1,2)");
            t.HasCheckConstraint("CK_MeisterTermin_Status", "[Status] IN (0,1,2,3)");
        });
        b.HasKey(e => e.TerminId);
        b.Property(e => e.TerminId).UseIdentityColumn();
        b.Property(e => e.TerminTyp).HasDefaultValue((byte)0);
        b.Property(e => e.Status).HasDefaultValue((byte)0);
        b.Property(e => e.NichtUebergebenGrund).HasMaxLength(500);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // Max. 1 Termin pro Typ pro Zuordnung
        b.HasIndex(e => new { e.ZuordnungId, e.TerminTyp }).IsUnique()
         .HasDatabaseName("UQ_MeisterTermin_Zuordnung_Typ");

        b.HasOne(e => e.Zuordnung)
         .WithMany(z => z.Termine)
         .HasForeignKey(e => e.ZuordnungId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

// ============================================================================
//  MeisterPatientenBuchungsposten (Snapshot – Trigger-geschützt)
// ============================================================================
public class MeisterPatientenBuchungspostenConfiguration : IEntityTypeConfiguration<MeisterPatientenBuchungsposten>
{
    public void Configure(EntityTypeBuilder<MeisterPatientenBuchungsposten> b)
    {
        b.ToTable("MeisterPatientenBuchungsposten", t => t.HasTrigger("TR_MeisterPatientenBuchungsposten_Protect"));
        b.HasKey(e => e.PostenId);
        b.Property(e => e.PostenId).UseIdentityColumn();
        b.Property(e => e.BelegNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.LehrgangNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.AbschnittBezeichnung).HasMaxLength(200).IsRequired();
        b.Property(e => e.PatientNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.PatientName).HasMaxLength(200).IsRequired();
        b.Property(e => e.MS1Nr).HasMaxLength(20).IsRequired();
        b.Property(e => e.MS1Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.MS2Nr).HasMaxLength(20);
        b.Property(e => e.MS2Name).HasMaxLength(200);
        b.Property(e => e.NichtUebergebenGrund).HasMaxLength(500);
        b.Property(e => e.GebuchtvonUser).HasMaxLength(100).IsRequired();
        b.Property(e => e.BuchungsDatum).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.GebuchtAm).HasDefaultValueSql("SYSUTCDATETIME()");
        // Kein FK (Snapshot-Prinzip)
    }
}
