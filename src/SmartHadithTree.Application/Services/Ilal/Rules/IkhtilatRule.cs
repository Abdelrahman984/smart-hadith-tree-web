using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Application.Services.Ilal.Rules;

/// <summary>
/// الاختلاط: a narration from a narrator whose memory deteriorated, by a student who heard
/// from him after the deterioration (or whose timing is unknown).
/// </summary>
public sealed class IkhtilatRule : IIlalRule
{
    public IEnumerable<IlalFindingDto> Evaluate(IlalContext context)
    {
        var occurrences = context.Chains
            .SelectMany(c => c.Links.Select(l => (Chain: c, Link: l)))
            .Where(x => context.Narrator(x.Link.SheikhId)?.IsMukhtalit == true)
            .GroupBy(x => (x.Link.SheikhId, x.Link.StudentId));

        foreach (var group in occurrences)
        {
            var (mukhtalitId, studentId) = group.Key;
            var mukhtalit = context.Narrator(mukhtalitId)!;
            var chains = group.Select(x => x.Chain).Distinct().ToList();
            var timing = context.Hearings.GetValueOrDefault((mukhtalitId, studentId), HearingTiming.Unknown);
            var allSahihayn = chains.All(c => TransmissionTerms.IsSahihayn(c.BookName));

            var note = string.IsNullOrWhiteSpace(mukhtalit.IkhtilatNote) ? "" : $" ({mukhtalit.IkhtilatNote})";
            var baseEvidence = $"{mukhtalit.Name} ممن اختلط{note}، والراوي عنه هنا {context.NameOf(studentId)}";

            IllahSeverity severity;
            string evidence;
            double confidence;

            switch (timing)
            {
                case HearingTiming.Before:
                    continue; // heard before the ikhtilat: sound

                case HearingTiming.After:
                    severity = allSahihayn ? IllahSeverity.GhayrQadihah : IllahSeverity.Qadihah;
                    evidence = $"{baseEvidence}، وقد نص العلماء على أنه سمع منه بعد الاختلاط."
                               + (allSahihayn ? " وأصحاب الصحيح ينتقون من حديثه ما ثبت أنه حدّث به قبل الاختلاط." : "");
                    confidence = allSahihayn ? 0.5 : 0.8;
                    break;

                default:
                    if (allSahihayn) continue; // the Sahihs only include what was heard before the ikhtilat
                    severity = IllahSeverity.Tanbih;
                    evidence = $"{baseEvidence}، ولم يتبين أسمع منه قبل الاختلاط أم بعده.";
                    confidence = 0.4;
                    break;
            }

            yield return new IlalFindingDto
            {
                Type = IllahType.Ikhtilat,
                Severity = severity,
                TitleAr = timing == HearingTiming.After ? "رواية بعد الاختلاط" : "رواية عن مختلط",
                EvidenceAr = evidence,
                NarratorIds = [mukhtalitId, studentId],
                HadithIds = chains.Select(c => c.HadithId).ToList(),
                Confidence = confidence
            };
        }
    }
}
