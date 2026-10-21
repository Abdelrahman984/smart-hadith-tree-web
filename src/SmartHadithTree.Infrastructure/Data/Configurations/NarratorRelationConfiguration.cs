using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="NarratorRelation"/> entity.
/// </summary>
public class NarratorRelationConfiguration : IEntityTypeConfiguration<NarratorRelation>
{
    public void Configure(EntityTypeBuilder<NarratorRelation> builder)
    {
        builder.ToTable("NarratorRelations");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Source)
            .IsRequired()
            .HasMaxLength(50);

        // Both FKs point to Narrators; RESTRICT, so deleting a narrator never silently deletes relations (it also
        // avoided SQL Server's multiple-cascade-paths error before the move to PostgreSQL).
        builder.HasOne<Narrator>()
            .WithMany()
            .HasForeignKey(r => r.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Narrator>()
            .WithMany()
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TeacherId, r.StudentId })
            .IsUnique()
            .HasDatabaseName("IX_NarratorRelations_TeacherId_StudentId");

        builder.HasIndex(r => new { r.StudentId, r.TeacherId })
            .HasDatabaseName("IX_NarratorRelations_StudentId_TeacherId");
    }
}
