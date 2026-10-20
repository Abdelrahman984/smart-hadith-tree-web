using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Infrastructure.Data;

/// <summary>
/// EF Core database context for the Smart Hadith Tree application.
/// Arabic text is searched through normalized columns (no tashkeel, unified alef/ta marbuta) with
/// pg_trgm indexes, not through a database collation.
/// </summary>
public class HadithTreeDbContext : DbContext, IHadithTreeDbContext
{
    public HadithTreeDbContext(DbContextOptions<HadithTreeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Narrator> Narrators => Set<Narrator>();
    public DbSet<HadithText> Hadiths => Set<HadithText>();
    public DbSet<Transmission> Transmissions => Set<Transmission>();
    public DbSet<ScholarEvaluation> ScholarEvaluations => Set<ScholarEvaluation>();
    public DbSet<HadithCluster> HadithClusters => Set<HadithCluster>();
    public DbSet<NarratorRelation> NarratorRelations => Set<NarratorRelation>();
    public DbSet<MukhtalitHearing> MukhtalitHearings => Set<MukhtalitHearing>();
    public DbSet<MukhtalitGroupRule> MukhtalitGroupRules => Set<MukhtalitGroupRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Trigram indexes (gin_trgm_ops) on the normalized search columns.
        modelBuilder.HasPostgresExtension("pg_trgm");

        // Apply all IEntityTypeConfiguration<T> implementations from this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HadithTreeDbContext).Assembly);
    }
}
