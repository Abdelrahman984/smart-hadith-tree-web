using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using System.Linq;
using System.Collections.Generic;

namespace SmartHadithTree.Application.Services;

public class TaqwiyahService : ITaqwiyahService
{
    // Simple Tier Mapping
    private int ParseTier(string? tierStr)
    {
        if (string.IsNullOrEmpty(tierStr)) return 7; // Default to T7 (Weak)
        var numPart = tierStr.Replace("T", "");
        if (int.TryParse(numPart, out int t)) return t;
        return 7;
    }

    public void CalculateTreeStrength(ComparativeTreeResponseDto tree)
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
                var nodeTier = MapGradeEnToTier(currentNode.GradeEn);
                
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

    private int MapGradeEnToTier(string? gradeEn)
    {
        if (string.IsNullOrEmpty(gradeEn)) return 7;
        var g = gradeEn.ToLower();
        if (g.Contains("companion")) return 1;
        if (g.Contains("reliable") && g.Contains("mostly")) return 4;
        if (g.Contains("reliable")) return 3;
        if (g.Contains("weak")) return 7;
        if (g.Contains("abandoned")) return 9;
        if (g.Contains("fabricator")) return 12;
        return 7;
    }
}
