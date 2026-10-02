using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="MukhtalitHearing"/> entity.
/// </summary>
public class MukhtalitHearingConfiguration : IEntityTypeConfiguration<MukhtalitHearing>
{
    public void Configure(EntityTypeBuilder<MukhtalitHearing> builder)
    {
        builder.ToTable("MukhtalitHearings");

        builder.HasKey(h => h.Id);

        builder.HasOne(h => h.Mukhtalit)
            .WithMany(n => n.MukhtalitHearings)
            .HasForeignKey(h => h.MukhtalitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Narrator>()
            .WithMany()
            .HasForeignKey(h => h.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.MukhtalitId, h.StudentId })
            .IsUnique()
            .HasDatabaseName("IX_MukhtalitHearings_MukhtalitId_StudentId");
    }
}
