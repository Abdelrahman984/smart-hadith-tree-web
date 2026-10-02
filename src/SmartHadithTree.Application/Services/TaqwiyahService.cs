using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using System.Linq;
using System.Collections.Generic;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Application.Services;

public class TaqwiyahService : ITaqwiyahService
{
    // Simple Tier Mapping
    private static int ParseTier(string? tierStr)
    {
        if (string.IsNullOrEmpty(tierStr)) return 7; // Default to T7 (Weak)
        var numPart = tierStr.Replace("T", "");
        if (int.TryParse(numPart, out int t)) return t;
        return 7;
    }

    public void CalculateTreeStrength(ComparativeTreeResponseDto tree)
    {
        CalculateStructuralStrength(tree);
        ApplyIlal(tree);
    }

    private static void CalculateStructuralStrength(ComparativeTreeResponseDto tree)
    {
        if (tree.Nodes == null || !tree.Nodes.Any())
            return;

        // Find root nodes (compilers)
        var compilers = tree.Nodes.Where(n => n.StepOrder == 1).ToList();
        
        var pathStrengths = new List<int>();

        // For each compiler, traverse up to the Sahabi (usually highest StepOrder)
        foreach (var compiler in compilers)
        {
            var lowestTierInPath = 1; // start strong
            var currentNode = compiler;
            
            while (currentNode != null)
            {
                // We assume there's an 'AiTier' or 'GradeEn' mapped to tier somewhere
                // Let's use a simple mapping from GradeEn to Tier for now if Tier is not explicit
                // Or we can assume we parse the AI tier if it's cached.
                // For demonstration, we map GradeEn to tier:
                var nodeTier = NarratorGradeScale.ToTier(currentNode.GradeEn);
                
                if (nodeTier > lowestTierInPath)
                    lowestTierInPath = nodeTier;

                if (currentNode.ParentNodeId.HasValue)
                {
                    currentNode = tree.Nodes.FirstOrDefault(n => n.Id == currentNode.ParentNodeId.Value);
                }
                else
                {
                    currentNode = null;
                }
            }
            
            pathStrengths.Add(lowestTierInPath);
        }

        // Taqwiyah Rules:
        // We have multiple path strengths. Each path is as strong as its weakest link (highest tier number).
        // Let's say:
        // If any path is <= T4, it's at least Hasan.
        // If we have two or more T7 (Weak) paths, they might upgrade to T4 (Hasan li-ghayrihi).
        // If all paths are T12 (Fabricated), it stays T12.

        if (!pathStrengths.Any())
        {
            tree.CalculatedGrade = "مجهول";
            return;
        }

        var bestPath = pathStrengths.Min(); // lowest tier number is best

        if (bestPath <= 3)
        {
            tree.CalculatedGrade = "صحيح";
            tree.TaqwiyahDetails = "يوجد إسناد صحيح مستقل.";
        }
        else if (bestPath == 4 || bestPath == 5)
        {
            tree.CalculatedGrade = "حسن";
            tree.TaqwiyahDetails = "يوجد إسناد حسن لذاته.";
        }
        else if (bestPath >= 6 && bestPath <= 8)
        {
            // Check for Taqwiyah
            var weakPathsCount = pathStrengths.Count(p => p >= 6 && p <= 8);
            if (weakPathsCount >= 2)
            {
                tree.CalculatedGrade = "حسن لغيره";
                tree.TaqwiyahDetails = $"ارتقى الحديث إلى الحسن لغيره بمجموع {weakPathsCount} طرق ضعيفة.";
            }
            else
            {
                tree.CalculatedGrade = "ضعيف";
                tree.TaqwiyahDetails = "إسناد ضعيف ولم يوجد ما يجبره.";
            }
        }
        else if (bestPath > 8)
        {
            tree.CalculatedGrade = "موضوع / متروك";
            tree.TaqwiyahDetails = "الحديث شديد الضعف أو موضوع، لا ينجبر بتعدد الطرق.";
        }
    }

    /// <summary>
    /// Adjusts the grade using the Ilal report: a hadith whose every tariq carries a decisive
    /// defect (علة قادحة) is ma'lul, regardless of how strong its narrators look.
    /// </summary>
    private static void ApplyIlal(ComparativeTreeResponseDto tree)
    {
        var report = tree.IlalReport;
        if (report == null || !report.HasQadihah) return;

        var qadihah = report.Findings.Where(f => f.Severity == IllahSeverity.Qadihah).ToList();
        var defectiveHadiths = qadihah.SelectMany(f => f.HadithIds).ToHashSet();
        var sourceIds = tree.Sources.Count > 0
            ? tree.Sources.Select(s => s.HadithId).ToList()
            : report.AnalyzedHadithIds;
        var titles = string.Join("، ", qadihah.Select(f => f.TitleAr).Distinct());

        if (sourceIds.Count > 0 && sourceIds.All(defectiveHadiths.Contains))
        {
            if (tree.CalculatedGrade == "موضوع / متروك") return;

            var apparent = tree.CalculatedGrade;
            tree.CalculatedGrade = "ضعيف (معلول)";
            tree.TaqwiyahDetails = string.IsNullOrEmpty(apparent)
                ? $"أُعلّ الحديث في جميع طرقه بـ: {titles}."
                : $"ظاهر الإسناد ({apparent})، لكن أُعلّ الحديث في جميع طرقه بـ: {titles}.";
        }
        else
        {
            tree.TaqwiyahDetails = $"{tree.TaqwiyahDetails} تنبيه: في بعض الطرق علة قادحة ({titles}).".Trim();
        }
    }
}
