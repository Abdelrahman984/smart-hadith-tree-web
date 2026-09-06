using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.DTOs;

using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Infrastructure.Data.Repositories;

public class HadithChainRepository(HadithTreeDbContext context) : IHadithChainRepository
{
    /// <summary>
    /// Traverses the Isnad graph for a given Hadith using a Recursive CTE,
    /// returning all narrators in the chain from the compiler up to the original source.
    /// </summary>
    public async Task<List<IsnadNodeDto>> GetIsnadTreeAsync(Guid hadithId, CancellationToken ct = default)
    {
        // We use a Recursive CTE to walk the graph.
        // Anchor: The Student in StepOrder 1 (the Compiler, e.g. Al-Bukhari).
        // Recursive: Join the Transmissions table where StudentId matches the parent's NarratorId.
        
        var sql = @"
            WITH RecursiveChain AS (
                -- Anchor: The Compiler (Student of Step 1)
                SELECT 
                    CAST('00000000-0000-0000-0000-000000000000' AS UNIQUEIDENTIFIER) AS Id,
                    t.StudentId AS NarratorId,
                    0 AS StepOrder,
                    CAST(NULL AS UNIQUEIDENTIFIER) AS ParentNodeId,
                    CAST(NULL AS NVARCHAR(50)) AS TransmissionTerm,
                    t.HadithId
                FROM Transmissions t
                WHERE t.HadithId = {0} AND t.StepOrder = 1

                UNION ALL

                -- Recursive: The Sheikh of the current Narrator
                SELECT 
                    child.Id,
                    child.SheikhId AS NarratorId,
                    child.StepOrder,
                    parent.Id AS ParentNodeId,
                    child.TransmissionTerm,
                    child.HadithId
                FROM Transmissions child
                INNER JOIN RecursiveChain parent 
                    ON child.StudentId = parent.NarratorId 
                    AND child.HadithId = parent.HadithId
            )
            SELECT 
                rc.Id,
                rc.NarratorId,
                n.FullName AS NarratorName,
                n.KnownAs,
                n.GenerationTier,
                rc.StepOrder,
                rc.ParentNodeId,
                rc.TransmissionTerm,
                n.ItqanGrade AS GradeEn,
                CAST(0 AS BIT) AS IsAnomaly,
                CAST(NULL AS NVARCHAR(MAX)) AS AnomalyReason
            FROM RecursiveChain rc
            INNER JOIN Narrators n ON rc.NarratorId = n.Id
            ORDER BY rc.StepOrder ASC;
        ";

        // Execute raw SQL mapping to the DTO directly
        var nodes = await context.Database
            .SqlQueryRaw<IsnadNodeDto>(sql, hadithId)
            .ToListAsync(ct);

        // Fetch birth/death years for the narrators in this chain
        var narratorIds = nodes.Select(n => n.NarratorId).Distinct().ToList();
        var narratorDates = await context.Narrators
            .Where(n => narratorIds.Contains(n.Id))
            .Select(n => new { n.Id, n.BirthYearHijri, n.DeathYearHijri })
            .ToDictionaryAsync(n => n.Id, ct);

        // Detect anomalies (Inqita' - Disconnection)
        foreach (var node in nodes)
        {
            if (node.ParentNodeId.HasValue && node.ParentNodeId.Value != Guid.Empty)
            {
                var studentNode = nodes.FirstOrDefault(n => n.Id == node.ParentNodeId.Value);
                if (studentNode != null && narratorDates.TryGetValue(node.NarratorId, out var sheikhDates) && narratorDates.TryGetValue(studentNode.NarratorId, out var studentDates))
                {
                    // If student was born AFTER sheikh died
                    if (studentDates.BirthYearHijri.HasValue && sheikhDates.DeathYearHijri.HasValue && 
                        studentDates.BirthYearHijri.Value > sheikhDates.DeathYearHijri.Value)
                    {
                        node.IsAnomaly = true;
                        node.AnomalyReason = $"انقطاع زمني: التلميذ ولد سنة {studentDates.BirthYearHijri} بعد وفاة الشيخ سنة {sheikhDates.DeathYearHijri}";
                    }
                }
            }
        }

        return nodes;
    }
}
