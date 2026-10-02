using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services.Ilal;

/// <summary>
/// Runs the Ilal rule engine over a set of turuq. Narrator metadata (mudallis tiers, ikhtilat,
/// teacher/student relations) is loaded in a few batched queries, then every rule is evaluated
/// in memory against the same <see cref="IlalContext"/>.
/// </summary>
public class IlalAnalysisService(IHadithTreeDbContext context) : IIlalAnalysisService
{
    /// <summary>The rules run for every analysis, in reporting order.</summary>
    public static IReadOnlyList<IIlalRule> DefaultRules { get; } =
    [
        new TadlisRule(),
        new IkhtilatRule(),
        new HiddenInqitaRule(),
        new MatnAtMadarRule(),
        new RafWaqfRule(),
        new WaslIrsalRule()
    ];

    public async Task<IlalReportDto> AnalyzeAsync(IReadOnlyCollection<Guid> hadithIds, CancellationToken ct = default)
    {
        var ilalContext = await LoadContextAsync(hadithIds, ct);
        return Analyze(ilalContext);
    }

    /// <summary>Runs all rules against an already-loaded context.</summary>
    public static IlalReportDto Analyze(IlalContext ilalContext, IEnumerable<IIlalRule>? rules = null)
    {
        var findings = (rules ?? DefaultRules)
            .SelectMany(r => r.Evaluate(ilalContext))
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.Confidence)
            .ToList();

        var madars = IsnadBranching.FindSplitPoints(ilalContext)
            .Select(s => new IlalMadarDto
            {
                NarratorId = s.MadarId,
                NarratorName = ilalContext.NameOf(s.MadarId),
                BranchCount = s.Branches.Count,
                HadithIds = s.AllChains.Select(c => c.HadithId).Distinct().ToList()
            })
            .OrderByDescending(m => m.HadithIds.Count)
            .ToList();

        return new IlalReportDto
        {
            AnalyzedHadithIds = ilalContext.Chains.Select(c => c.HadithId).ToList(),
            Turuq = ilalContext.Chains.Select(c => new IlalTariqDto
            {
                HadithId = c.HadithId,
                BookName = c.BookName,
                HadithNumber = c.HadithNumber,
                IsMarfu = MatnText.IsMarfu(c.MatnArabic)
            }).ToList(),
            Madars = madars,
            Findings = findings,
            HasQadihah = findings.Any(f => f.Severity == IllahSeverity.Qadihah),
            SummaryAr = Summarize(findings, ilalContext.Chains.Count)
        };
    }

    private async Task<IlalContext> LoadContextAsync(IReadOnlyCollection<Guid> hadithIds, CancellationToken ct)
    {
        var ids = hadithIds.Distinct().ToList();

        var hadiths = await context.Hadiths.AsNoTracking()
            .Where(h => ids.Contains(h.Id))
            .Select(h => new { h.Id, h.BookName, h.HadithNumber, h.MatnArabic })
            .ToListAsync(ct);

        var transmissions = await context.Transmissions.AsNoTracking()
            .Where(t => ids.Contains(t.HadithId))
            .Select(t => new { t.HadithId, t.StudentId, t.SheikhId, t.TransmissionTerm, t.StepOrder })
            .ToListAsync(ct);

        var linksByHadith = transmissions
            .GroupBy(t => t.HadithId)
            .ToDictionary(g => g.Key, g => g.Select(t => new IlalLink(t.StudentId, t.SheikhId, t.TransmissionTerm, t.StepOrder)).ToList());

        var chains = hadiths
            .Select(h => new IlalChain
            {
                HadithId = h.Id,
                BookName = h.BookName,
                HadithNumber = h.HadithNumber,
                MatnArabic = h.MatnArabic,
                Links = BuildPath(linksByHadith.GetValueOrDefault(h.Id) ?? [])
            })
            .Where(c => c.Links.Count > 0)
            .ToList();

        var narratorIds = chains.SelectMany(c => c.Path).Distinct().ToList();

        var narrators = await context.Narrators.AsNoTracking()
            .Where(n => narratorIds.Contains(n.Id))
            .Select(n => new IlalNarrator(
                n.Id,
                n.KnownAs ?? n.FullName,
                n.ItqanGrade,
                n.GenerationTier,
                n.MudallisTier,
                n.HasMukhtalit,
                n.IkhtilatNote))
            .ToDictionaryAsync(n => n.Id, ct);

        var relations = await context.NarratorRelations.AsNoTracking()
            .Where(r => narratorIds.Contains(r.TeacherId) && narratorIds.Contains(r.StudentId))
            .Select(r => new { r.TeacherId, r.StudentId })
            .ToListAsync(ct);

        var teachersWithData = await context.NarratorRelations.AsNoTracking()
            .Where(r => narratorIds.Contains(r.TeacherId))
            .Select(r => r.TeacherId)
            .Distinct()
            .ToListAsync(ct);

        var studentsWithData = await context.NarratorRelations.AsNoTracking()
            .Where(r => narratorIds.Contains(r.StudentId))
            .Select(r => r.StudentId)
            .Distinct()
            .ToListAsync(ct);

        var hearings = await context.MukhtalitHearings.AsNoTracking()
            .Where(h => narratorIds.Contains(h.MukhtalitId))
            .Select(h => new { h.MukhtalitId, h.StudentId, h.Timing })
            .ToListAsync(ct);

        return new IlalContext
        {
            Chains = chains,
            Narrators = narrators,
            Relations = relations.Select(r => (r.TeacherId, r.StudentId)).ToHashSet(),
            NarratorsWithRelations = teachersWithData.Concat(studentsWithData).ToHashSet(),
            Hearings = hearings.ToDictionary(h => (h.MukhtalitId, h.StudentId), h => h.Timing)
        };
    }

    /// <summary>
    /// Builds a single path from the compiler upward. When a chain branches (several links at the
    /// same step), the link whose student is the previous sheikh is followed.
    /// </summary>
    internal static List<IlalLink> BuildPath(IReadOnlyCollection<IlalLink> links)
    {
        var path = new List<IlalLink>();
        var current = links.Where(l => l.StepOrder == 1).FirstOrDefault();
        var visited = new HashSet<Guid>();

        while (current != null && visited.Add(current.SheikhId))
        {
            path.Add(current);
            var next = current;
            current = links.FirstOrDefault(l => l.StepOrder == next.StepOrder + 1 && l.StudentId == next.SheikhId);
        }

        return path;
    }

    private static string Summarize(IReadOnlyCollection<IlalFindingDto> findings, int chainCount)
    {
        if (chainCount == 0)
            return "لا توجد أسانيد مستخرجة لهذه الأحاديث، فتعذّر فحص العلل.";

        if (findings.Count == 0)
            return chainCount == 1
                ? "لم تظهر علة في هذا الإسناد بحسب القواعد الآلية. ويُستحسن جمع الطرق للكشف عن العلل الخفية."
                : $"لم تظهر علة في الطرق المجموعة ({chainCount}) بحسب القواعد الآلية.";

        var qadihah = findings.Count(f => f.Severity == IllahSeverity.Qadihah);
        var ghayr = findings.Count(f => f.Severity == IllahSeverity.GhayrQadihah);
        var tanbih = findings.Count(f => f.Severity == IllahSeverity.Tanbih);

        var parts = new List<string>();
        if (qadihah > 0) parts.Add($"{qadihah} علة قادحة");
        if (ghayr > 0) parts.Add($"{ghayr} علة غير قادحة");
        if (tanbih > 0) parts.Add($"{tanbih} تنبيه");

        return $"ظهر بعد فحص {chainCount} {(chainCount == 1 ? "طريق" : "طرق")}: {string.Join("، ", parts)}. وهذه نتائج آلية تُعين المحقق ولا تغني عن نظره.";
    }
}
