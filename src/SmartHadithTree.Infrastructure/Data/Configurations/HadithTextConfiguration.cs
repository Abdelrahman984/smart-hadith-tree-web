using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="HadithText"/> entity.
/// </summary>
public class HadithTextConfiguration : IEntityTypeConfiguration<HadithText>
{
    public void Configure(EntityTypeBuilder<HadithText> builder)
    {
        builder.ToTable("Hadiths");

        // ── Primary Key ────────────────────────────────────────────
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            ;

        // ── String Properties ──────────────────────────────────────
        builder.Property(h => h.MatnArabic)
            .IsRequired();
        builder.Property(h => h.NormalizedMatn)
            .IsRequired()
            .UseCollation("C");
        // MatnArabic is nvarchar(max) by default — Hadith texts can be lengthy.

        builder.Property(h => h.BookName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(h => h.NormalizedBookName)
            .IsRequired()
            .HasMaxLength(300)
            .UseCollation("C");

        builder.Property(h => h.Volume)
            .HasMaxLength(50);

        builder.Property(h => h.Chapter)
            .HasMaxLength(500);

        builder.Property(h => h.FullIsnadText)
            .UseCollation("C");

        // ── Indexes ────────────────────────────────────────────────
        // Composite index for lookup-by-reference (book + number).
        builder.HasIndex(h => new { h.BookName, h.HadithNumber })
            .HasDatabaseName("IX_Hadiths_BookName_HadithNumber");

        // HadithNumber alone for direct-number searches.
        builder.HasIndex(h => h.HadithNumber)
            .HasDatabaseName("IX_Hadiths_HadithNumber");

        // NormalizedBookName index for fast book filtering.
        builder.HasIndex(h => h.NormalizedBookName)
            .HasDatabaseName("IX_Hadiths_NormalizedBookName");

        // Trigram (pg_trgm) indexes for the substring searches of HadithSearchService (Contains / LIKE '%x%').
        builder.HasIndex(h => h.NormalizedMatn)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_Hadiths_NormalizedMatn_Trgm");

        builder.HasIndex(h => h.FullIsnadText)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_Hadiths_FullIsnadText_Trgm");

        // Named overload: a second unnamed HasIndex on NormalizedBookName would reconfigure the B-tree index above.
        builder.HasIndex(h => h.NormalizedBookName, "IX_Hadiths_NormalizedBookName_Trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
