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
                rc.TransmissionTerm
            FROM RecursiveChain rc
            INNER JOIN Narrators n ON rc.NarratorId = n.Id
            ORDER BY rc.StepOrder ASC;
        ";

        // Execute raw SQL mapping to the DTO directly
        var nodes = await context.Database
            .SqlQueryRaw<IsnadNodeDto>(sql, hadithId)
            .ToListAsync(ct);

        return nodes;
    }
}
