using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schulverwaltung.Domain.Entities;

namespace Schulverwaltung.Infrastructure.Configurations;

// ============================================================================
//  Person
// ============================================================================
public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> b)
    {
        b.ToTable("Person", t => t.HasTrigger("TR_Person_ModifiedAt"));
        b.HasKey(e => e.PersonId);
        b.Property(e => e.PersonId).UseIdentityColumn();
        b.Property(e => e.PersonNr).HasMaxLength(20).IsRequired();
        b.HasIndex(e => e.PersonNr).IsUnique();
        b.Property(e => e.Anrede).HasMaxLength(20);
        b.Property(e => e.Titel).HasMaxLength(50);
        b.Property(e => e.Vorname).HasMaxLength(100).IsRequired();
        b.Property(e => e.Nachname).HasMaxLength(100).IsRequired();
        b.Property(e => e.Namenszusatz).HasMaxLength(50);
        b.Property(e => e.Geburtsname).HasMaxLength(100);
        b.Property(e => e.Geburtsort).HasMaxLength(100);
        b.Property(e => e.Nationalitaet).HasMaxLength(100).HasDefaultValue("deutsch");
        b.Property(e => e.FotoTyp).HasMaxLength(50);
        b.Property(e => e.Strasse).HasMaxLength(200);
        b.Property(e => e.PLZ).HasMaxLength(10);
        b.Property(e => e.Ort).HasMaxLength(100);
        b.Property(e => e.Land).HasMaxLength(50).HasDefaultValue("Deutschland");
        b.Property(e => e.Email).HasMaxLength(200);
        b.Property(e => e.Telefon).HasMaxLength(30);
        b.Property(e => e.Mobil).HasMaxLength(30);
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.Ignore(e => e.VollerName);
        b.Ignore(e => e.AnzeigeName);
    }
}

// ============================================================================
//  PersonRolle
// ============================================================================
public class PersonRolleConfiguration : IEntityTypeConfiguration<PersonRolle>
{
    public void Configure(EntityTypeBuilder<PersonRolle> b)
    {
        b.ToTable("PersonRolle", t => t.HasTrigger("TR_PersonRolle_ModifiedAt"));
        b.HasKey(e => e.PersonRolleId);
        b.Property(e => e.PersonRolleId).UseIdentityColumn();
        b.Property(e => e.RolleTyp).HasConversion<byte>();
        b.Property(e => e.Status).HasDefaultValue((byte)0);
        b.Property(e => e.GueltigAb).HasDefaultValueSql("CAST(SYSUTCDATETIME() AS DATE)");
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // Gefilterter Unique Index: eine Rolle pro Person nur einmal aktiv (Status=0)
        b.HasIndex(e => new { e.PersonId, e.RolleTyp })
         .IsUnique()
         .HasFilter("[Status] = 0");

        b.HasOne(e => e.Person)
         .WithMany(p => p.Rollen)
         .HasForeignKey(e => e.PersonId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.Betrieb)
         .WithMany(b2 => b2.PersonRollen)
         .HasForeignKey(e => e.BetriebId)
         .OnDelete(DeleteBehavior.SetNull);
    }
}

// ============================================================================
//  PersonDozentProfil (1:1 mit Person, PersonId ist PK+FK)
// ============================================================================
public class PersonDozentProfilConfiguration : IEntityTypeConfiguration<PersonDozentProfil>
{
    public void Configure(EntityTypeBuilder<PersonDozentProfil> b)
    {
        b.ToTable("PersonDozentProfil");
        b.HasKey(e => e.PersonId);
        b.Property(e => e.PersonId).ValueGeneratedNever();
        b.Property(e => e.Kuerzel).HasMaxLength(10);
        b.Property(e => e.Bemerkungen).HasColumnType("nvarchar(max)");
        b.Property(e => e.MaxStundenProWoche).HasColumnType("decimal(5,2)");
        b.Property(e => e.IstOrthopaedie).HasDefaultValue(false);
        b.Property(e => e.IstPodologie).HasDefaultValue(false);
        b.Property(e => e.IstMedizin).HasDefaultValue(false);
        b.Property(e => e.KostenTheoriestunde).HasColumnType("decimal(10,2)");
        b.Property(e => e.KostenPraxisstunde).HasColumnType("decimal(10,2)");
        b.Property(e => e.Fahrtkosten).HasColumnType("decimal(10,2)");
        b.Property(e => e.IBAN).HasMaxLength(34);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(e => e.Person)
         .WithOne()
         .HasForeignKey<PersonDozentProfil>(e => e.PersonId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

// ============================================================================
//  PersonPatientProfil (1:1 mit Person, PersonId ist PK+FK)
// ============================================================================
public class PersonPatientProfilConfiguration : IEntityTypeConfiguration<PersonPatientProfil>
{
    public void Configure(EntityTypeBuilder<PersonPatientProfil> b)
    {
        b.ToTable("PersonPatientProfil");
        b.HasKey(e => e.PersonId);
        b.Property(e => e.PersonId).ValueGeneratedNever();
        b.Property(e => e.Gewicht).HasColumnType("decimal(5,1)");
        b.Property(e => e.IstDiabetiker).HasDefaultValue(false);
        b.Property(e => e.GeeignetPV1).HasDefaultValue(false);
        b.Property(e => e.GeeignetPV2).HasDefaultValue(false);
        b.Property(e => e.GeeignetPV3).HasDefaultValue(false);
        b.Property(e => e.GeeignetPV4).HasDefaultValue(false);
        b.Property(e => e.GeeignetPVPruefung).HasDefaultValue(false);
        b.Property(e => e.Bemerkungen).HasColumnType("nvarchar(max)");
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(e => e.Person)
         .WithOne(p => p.PatientProfil)
         .HasForeignKey<PersonPatientProfil>(e => e.PersonId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

// ============================================================================
//  PersonAenderungsposten (unveränderlich – kein Update / Delete via EF)
// ============================================================================
public class PersonAenderungspostenConfiguration : IEntityTypeConfiguration<PersonAenderungsposten>
{
    public void Configure(EntityTypeBuilder<PersonAenderungsposten> b)
    {
        b.ToTable("PersonAenderungsposten");
        b.HasKey(e => e.PostenId);
        b.Property(e => e.PostenId).UseIdentityColumn();
        b.Property(e => e.BelegNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.PersonNr).HasMaxLength(20).IsRequired();
        b.Property(e => e.PersonName).HasMaxLength(200).IsRequired();
        b.Property(e => e.Ereignis).HasMaxLength(100).IsRequired();
        b.Property(e => e.Tabelle).HasMaxLength(100).IsRequired();
        b.Property(e => e.Feld).HasMaxLength(100);
        b.Property(e => e.AlterWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.NeuerWert).HasColumnType("nvarchar(max)");
        b.Property(e => e.RolleTyp).HasConversion<byte?>();
        b.Property(e => e.Zeitstempel).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.AusfuehrendUser).HasMaxLength(200).IsRequired();
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");

        // Keine FK-Navigation zu Person (Snapshot-Prinzip – Posten überleben Personenlöschung)
    }
}
