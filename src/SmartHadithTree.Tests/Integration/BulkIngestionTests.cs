using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Etl.Parsers;
using SmartHadithTree.Etl.Services;

namespace SmartHadithTree.Tests.Integration;

/// <summary>The ETL's binary COPY (EFCore.BulkExtensions.PostgreSql) into a real database.</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class BulkIngestionTests(PostgresFixture pg)
{
    [Fact]
    public async Task IngestAsync_CopiesEveryTable_AndStoresTheNormalizedNames()
    {
        var student = new Narrator { Id = Guid.NewGuid(), FullName = "مُحَمَّدُ بْنُ إِسْمَاعِيلَ الْبُخَارِيُّ", SourceKey = $"b{Guid.NewGuid():N}"[..12] };
        var sheikh = new Narrator { Id = Guid.NewGuid(), FullName = "عَبْدُ اللَّهِ بْنُ يُوسُفَ", KnownAs = "التِّنِّيسِيُّ", SourceKey = $"b{Guid.NewGuid():N}"[..12] };
        var hadith = new HadithText
        {
            Id = Guid.NewGuid(), BookName = "صحيح البخاري", NormalizedBookName = "صحيح البخاري", HadithNumber = 1,
            MatnArabic = "إِنَّمَا الْأَعْمَالُ بِالنِّيَّاتِ", NormalizedMatn = "انما الاعمال بالنيات", FullIsnadText = "حدثنا عبد الله بن يوسف"
        };
        var dataset = new ParsedDataset
        {
            Narrators = [student, sheikh],
            Hadiths = [hadith],
            Transmissions = [new Transmission { Id = Guid.NewGuid(), HadithId = hadith.Id, StudentId = student.Id, SheikhId = sheikh.Id, StepOrder = 1, TransmissionTerm = "حدثنا" }],
            ScholarEvaluations = [new ScholarEvaluation { Id = Guid.NewGuid(), NarratorId = sheikh.Id, ScholarName = "ابن حجر", EvaluationText = "ثقة متقن", VerdictRating = "ثقة" }],
            NarratorRelations = [new NarratorRelation { Id = Guid.NewGuid(), TeacherId = sheikh.Id, StudentId = student.Id, Source = "test" }]
        };

        await using (var db = pg.CreateContext())
            await new BulkDataIngestionService(db, NullLogger<BulkDataIngestionService>.Instance).IngestAsync(dataset);

        await using var check = pg.CreateContext();
        (await check.Narrators.CountAsync(n => n.Id == student.Id || n.Id == sheikh.Id)).Should().Be(2);
        (await check.Hadiths.CountAsync(h => h.Id == hadith.Id)).Should().Be(1);
        (await check.Transmissions.CountAsync(t => t.HadithId == hadith.Id)).Should().Be(1);
        (await check.ScholarEvaluations.CountAsync(e => e.NarratorId == sheikh.Id)).Should().Be(1);
        (await check.NarratorRelations.CountAsync(r => r.TeacherId == sheikh.Id)).Should().Be(1);

        // The computed normalized columns reach the table through COPY too (read back with raw SQL, not the getter).
        var stored = await check.Database
            .SqlQuery<string>($"""SELECT "NormalizedFullName" || '|' || COALESCE("NormalizedKnownAs", '') AS "Value" FROM "Narrators" WHERE "Id" = {sheikh.Id}""")
            .SingleAsync();
        stored.Should().Be("عبد الله بن يوسف|التنيسي");
    }
}
