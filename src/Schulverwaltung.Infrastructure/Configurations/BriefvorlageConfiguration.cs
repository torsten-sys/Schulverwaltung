using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schulverwaltung.Domain.Entities;

namespace Schulverwaltung.Infrastructure.Configurations;

public class BriefvorlageConfiguration : IEntityTypeConfiguration<Briefvorlage>
{
    public void Configure(EntityTypeBuilder<Briefvorlage> b)
    {
        b.ToTable("Briefvorlage", t => t.HasTrigger("TR_Briefvorlage_ModifiedAt"));
        b.HasKey(e => e.BriefvorlageId);
        b.Property(e => e.BriefvorlageId).UseIdentityColumn();
        b.Property(e => e.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(e => e.KopfHtml).HasColumnType("nvarchar(max)");
        b.Property(e => e.FussHtml).HasColumnType("nvarchar(max)");
        b.Property(e => e.Notiz).HasColumnType("nvarchar(max)");
        b.Property(e => e.IstStandard).HasDefaultValue(false);
        b.Property(e => e.Gesperrt).HasDefaultValue(false);
        b.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(e => e.ModifiedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
