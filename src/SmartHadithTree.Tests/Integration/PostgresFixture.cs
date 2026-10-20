using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace SmartHadithTree.Tests.Integration;

/// <summary>
/// One PostgreSQL 18 container for the whole test run, migrated with the real migrations. The InMemory provider
/// does not run raw SQL or translate queries, so these tests cover what it cannot: the isnad CTEs, LIKE/trigram
/// search, collations, and the binary COPY of the ETL. Needs Docker; without it run
/// <c>dotnet test --filter "Category!=Integration"</c>.
/// Tests share the database: each one seeds its own rows (fresh GUIDs, its own marker words) and only queries those.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public HadithTreeDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HadithTreeDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}

/// <summary>Made-up Arabic words, so a test's search terms match only the rows it seeded itself.</summary>
internal static class Marker
{
    // No letters that ArabicNormalizer rewrites (أ إ آ ة), so the word is the same before and after normalizing.
    private const string Letters = "بتثجحخدذرزسشصضطظعغفقكلمنهوي";

    public static string New() =>
        string.Concat(Guid.NewGuid().ToByteArray().Take(7).Select(b => Letters[b % Letters.Length]));

    /// <summary>The word with a fatha on every letter (what a user may type, and what the normalizer strips).</summary>
    public static string WithTashkeel(string word) => string.Concat(word.Select(c => $"{c}َ"));
}

/// <summary>Small builder for narrators, hadiths and their chains, saved in one call.</summary>
internal sealed class ChainSeed(HadithTreeDbContext db)
{
    public Narrator Narrator(string name, string? sourceKey = null, int? ibnHajarRank = null, string? knownAs = null)
    {
        var n = new Narrator
        {
            Id = Guid.NewGuid(),
            FullName = name,
            KnownAs = knownAs,
            SourceKey = sourceKey ?? $"t{Guid.NewGuid():N}"[..12],
            IbnHajarRank = ibnHajarRank
        };
        db.Narrators.Add(n);
        return n;
    }

    public HadithText Hadith(string book, int number, string matn)
    {
        var h = new HadithText
        {
            Id = Guid.NewGuid(),
            BookName = book,
            NormalizedBookName = SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(book),
            HadithNumber = number,
            MatnArabic = matn,
            NormalizedMatn = SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(matn)
        };
        db.Hadiths.Add(h);
        return h;
    }

    public Transmission Link(HadithText hadith, Narrator student, Narrator sheikh, int step, string term = "حدثنا")
    {
        var t = new Transmission
        {
            Id = Guid.NewGuid(),
            HadithId = hadith.Id,
            StudentId = student.Id,
            SheikhId = sheikh.Id,
            StepOrder = step,
            TransmissionTerm = term
        };
        db.Transmissions.Add(t);
        return t;
    }

    public Task SaveAsync() => db.SaveChangesAsync();
}
