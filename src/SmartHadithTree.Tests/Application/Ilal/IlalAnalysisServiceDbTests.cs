using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Infrastructure.Data;

namespace SmartHadithTree.Tests.Application.Ilal;

public class IlalAnalysisServiceDbTests
{
    [Fact]
    public async Task AnalyzeAsync_LoadsChainsAndNarratorFlagsFromDatabase()
    {
        var options = new DbContextOptionsBuilder<HadithTreeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new HadithTreeDbContext(options);

        Narrator N(string name, int? mudallisTier = null) => new()
        {
            Id = Guid.NewGuid(), FullName = name, ItqanGrade = "reliable",
            IsMudallis = mudallisTier.HasValue, MudallisTier = mudallisTier
        };
        var compiler = N("أبو داود");
        var shuba = N("شعبة");
        var qatada = N("قتادة", mudallisTier: 3);
        var anas = N("أنس");
        db.Narrators.AddRange(compiler, shuba, qatada, anas);

        var hadith = new HadithText
        {
            Id = Guid.NewGuid(), BookName = "سنن أبي داود", HadithNumber = 1,
            MatnArabic = "حدثنا شعبة عن قتادة عن أنس قال قال رسول الله ﷺ ...",
            NormalizedMatn = "", NormalizedBookName = ""
        };
        db.Hadiths.Add(hadith);
        db.Transmissions.AddRange(
            new Transmission { Id = Guid.NewGuid(), HadithId = hadith.Id, StepOrder = 1, StudentId = compiler.Id, SheikhId = shuba.Id, TransmissionTerm = "حدثنا" },
            new Transmission { Id = Guid.NewGuid(), HadithId = hadith.Id, StepOrder = 2, StudentId = shuba.Id, SheikhId = qatada.Id, TransmissionTerm = "عن" },
            new Transmission { Id = Guid.NewGuid(), HadithId = hadith.Id, StepOrder = 3, StudentId = qatada.Id, SheikhId = anas.Id, TransmissionTerm = "عن" });
        await db.SaveChangesAsync();

        var report = await new IlalAnalysisService(db).AnalyzeAsync([hadith.Id]);

        report.AnalyzedHadithIds.Should().Equal(hadith.Id);
        report.Findings.Should().ContainSingle(f => f.Type == IllahType.Tadlis && f.Severity == IllahSeverity.Qadihah);
        report.HasQadihah.Should().BeTrue();
    }
}
