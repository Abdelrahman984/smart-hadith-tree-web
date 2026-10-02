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
                    AND child.StepOrder = parent.StepOrder + 1
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
                n.IsMudallis,
                n.HasMukhtalit,
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

    /// <summary>
    /// Traverses the Isnad graphs for multiple Hadiths using a multi-anchor Recursive CTE,
    /// merging shared narrators into unified nodes with source attribution.
    /// </summary>
    public async Task<List<ComparativeIsnadNodeDto>> GetComparativeIsnadTreeAsync(
        List<Guid> hadithIds, CancellationToken ct = default)
    {
        if (hadithIds.Count == 0) return [];

        // Build parameterized placeholders for the IN clause
        var paramPlaceholders = string.Join(", ", hadithIds.Select((_, i) => $"{{{i}}}"));
        var parameters = hadithIds.Cast<object>().ToArray();

        var sql = $@"
            WITH RecursiveChain AS (
                -- Anchor: The Compiler (Student of Step 1) for ALL selected Hadiths
                SELECT 
                    NEWID() AS Id,
                    t.StudentId AS NarratorId,
                    0 AS StepOrder,
                    CAST(NULL AS UNIQUEIDENTIFIER) AS ParentNodeId,
                    CAST(NULL AS NVARCHAR(50)) AS TransmissionTerm,
                    t.HadithId
                FROM Transmissions t
                WHERE t.HadithId IN ({paramPlaceholders}) AND t.StepOrder = 1

                UNION ALL

                -- Recursive: The Sheikh of the current Narrator (per-Hadith chain)
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
                    AND child.StepOrder = parent.StepOrder + 1
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
                n.IsMudallis,
                n.HasMukhtalit,
                CAST(0 AS BIT) AS IsAnomaly,
                CAST(NULL AS NVARCHAR(MAX)) AS AnomalyReason,
                rc.HadithId AS SourceHadithId,
                h.BookName AS SourceBookName
            FROM RecursiveChain rc
            INNER JOIN Narrators n ON rc.NarratorId = n.Id
            INNER JOIN Hadiths h ON rc.HadithId = h.Id
            ORDER BY rc.StepOrder ASC;
        ";

        // Execute raw SQL — returns flat rows with per-hadith attribution
        var rawRows = await context.Database
            .SqlQueryRaw<ComparativeRawRow>(sql, parameters)
            .ToListAsync(ct);

        // Merge: group by NarratorId to collapse shared nodes
        var mergedNodes = new List<ComparativeIsnadNodeDto>();
        var narratorGroups = rawRows.GroupBy(r => r.NarratorId);

        foreach (var group in narratorGroups)
        {
            var first = group.First();
            mergedNodes.Add(new ComparativeIsnadNodeDto
            {
                Id = first.Id,
                NarratorId = first.NarratorId,
                NarratorName = first.NarratorName,
                KnownAs = first.KnownAs,
                GenerationTier = first.GenerationTier,
                StepOrder = group.Min(r => r.StepOrder),
                ParentNodeId = first.ParentNodeId,
                TransmissionTerm = first.TransmissionTerm,
                GradeEn = first.GradeEn,
                IsMudallis = first.IsMudallis,
                HasMukhtalit = first.HasMukhtalit,
                SourceHadithIds = group.Select(r => r.SourceHadithId).Distinct().ToList(),
                SourceBooks = group.Select(r => r.SourceBookName).Distinct().ToList()
            });
        }

        // Build edges from the raw rows (preserving per-hadith parent links)
        // We need edges that connect narrator->narrator, deduplicating across books
        var edgeNodes = new List<ComparativeIsnadNodeDto>();
        var seenEdges = new HashSet<string>();

        foreach (var row in rawRows)
        {
            if (row.ParentNodeId.HasValue && row.ParentNodeId.Value != Guid.Empty)
            {
                // Find the parent row to get its NarratorId
                var parentRow = rawRows.FirstOrDefault(r => r.Id == row.ParentNodeId.Value);
                if (parentRow != null)
                {
                    var edgeKey = $"{row.NarratorId}->{parentRow.NarratorId}";
                    if (!seenEdges.Contains(edgeKey))
                    {
                        seenEdges.Add(edgeKey);
                        // Update the merged node's ParentNodeId to point to the parent's merged node Id
                        var mergedNode = mergedNodes.FirstOrDefault(n => n.NarratorId == row.NarratorId);
                        var mergedParent = mergedNodes.FirstOrDefault(n => n.NarratorId == parentRow.NarratorId);
                        if (mergedNode != null && mergedParent != null)
                        {
                            // Create an edge-representing node entry that preserves the link
                            // The frontend uses ParentNodeId to find the target narrator
                        }
                    }
                }
            }
        }

        // Reconstruct ParentNodeId references to use merged node Ids
        // Build a mapping: for each raw row, map (NarratorId, HadithId) -> merged node
        foreach (var node in mergedNodes)
        {
            // Find a raw row for this narrator that has a valid parent
            var rowWithParent = rawRows
                .Where(r => r.NarratorId == node.NarratorId && r.ParentNodeId.HasValue && r.ParentNodeId.Value != Guid.Empty)
                .FirstOrDefault();

            if (rowWithParent != null)
            {
                var parentRow = rawRows.FirstOrDefault(r => r.Id == rowWithParent.ParentNodeId!.Value);
                if (parentRow != null)
                {
                    var mergedParent = mergedNodes.FirstOrDefault(n => n.NarratorId == parentRow.NarratorId);
                    if (mergedParent != null)
                    {
                        node.ParentNodeId = mergedParent.Id;
                    }
                }
            }
            else
            {
                node.ParentNodeId = null;
            }
        }

        // However, a narrator can have MULTIPLE parents across different books
        // (e.g., al-Zuhri receives from both Sa'id ibn al-Musayyib in one chain 
        //  and from 'Urwah in another). We need additional entries for those extra edges.
        var additionalEdgeNodes = new List<ComparativeIsnadNodeDto>();
        var primaryParents = new Dictionary<Guid, Guid>(); // narratorId -> first parentNarratorId

        foreach (var node in mergedNodes)
        {
            if (node.ParentNodeId.HasValue)
            {
                var parentNarrator = mergedNodes.FirstOrDefault(n => n.Id == node.ParentNodeId.Value);
                if (parentNarrator != null)
                {
                    primaryParents[node.NarratorId] = parentNarrator.NarratorId;
                }
            }
        }

        // Find additional parent links from other chains
        foreach (var row in rawRows)
        {
            if (!row.ParentNodeId.HasValue || row.ParentNodeId.Value == Guid.Empty) continue;

            var parentRow = rawRows.FirstOrDefault(r => r.Id == row.ParentNodeId.Value);
            if (parentRow == null) continue;

            // Check if this parent is different from the primary parent
            if (primaryParents.TryGetValue(row.NarratorId, out var primaryParentNarratorId))
            {
                if (parentRow.NarratorId != primaryParentNarratorId)
                {
                    var edgeKey = $"extra-{row.NarratorId}->{parentRow.NarratorId}";
                    if (!seenEdges.Contains(edgeKey))
                    {
                        seenEdges.Add(edgeKey);
                        var mergedParent = mergedNodes.FirstOrDefault(n => n.NarratorId == parentRow.NarratorId);
                        if (mergedParent != null)
                        {
                            // Add a duplicate node entry that represents this additional edge
                            additionalEdgeNodes.Add(new ComparativeIsnadNodeDto
                            {
                                Id = Guid.NewGuid(),
                                NarratorId = row.NarratorId,
                                NarratorName = row.NarratorName,
                                KnownAs = row.KnownAs,
                                GenerationTier = row.GenerationTier,
                                StepOrder = row.StepOrder,
                                ParentNodeId = mergedParent.Id,
                                TransmissionTerm = row.TransmissionTerm,
                                GradeEn = row.GradeEn,
                                IsMudallis = row.IsMudallis,
                                HasMukhtalit = row.HasMukhtalit,
                                SourceHadithIds = [row.SourceHadithId],
                                SourceBooks = [row.SourceBookName]
                            });
                        }
                    }
                }
            }
        }

        mergedNodes.AddRange(additionalEdgeNodes);

        // Detect anomalies (Inqita') using the same logic as single-chain
        var narratorIds = mergedNodes.Select(n => n.NarratorId).Distinct().ToList();
        var narratorDates = await context.Narrators
            .Where(n => narratorIds.Contains(n.Id))
            .Select(n => new { n.Id, n.BirthYearHijri, n.DeathYearHijri })
            .ToDictionaryAsync(n => n.Id, ct);

        foreach (var node in mergedNodes)
        {
            if (node.ParentNodeId.HasValue && node.ParentNodeId.Value != Guid.Empty)
            {
                var studentNode = mergedNodes.FirstOrDefault(n => n.Id == node.ParentNodeId.Value);
                if (studentNode != null &&
                    narratorDates.TryGetValue(node.NarratorId, out var sheikhDates) &&
                    narratorDates.TryGetValue(studentNode.NarratorId, out var studentDates))
                {
                    if (studentDates.BirthYearHijri.HasValue && sheikhDates.DeathYearHijri.HasValue &&
                        studentDates.BirthYearHijri.Value > sheikhDates.DeathYearHijri.Value)
                    {
                        node.IsAnomaly = true;
                        node.AnomalyReason = $"انقطاع زمني: التلميذ ولد سنة {studentDates.BirthYearHijri} بعد وفاة الشيخ سنة {sheikhDates.DeathYearHijri}";
                    }
                }
            }
        }

        return mergedNodes;
    }
}

/// <summary>
/// Internal DTO for raw CTE result rows before merging.
/// </summary>
internal class ComparativeRawRow
{
    public Guid Id { get; set; }
    public Guid NarratorId { get; set; }
    public string NarratorName { get; set; } = string.Empty;
    public string? KnownAs { get; set; }
    public string? GenerationTier { get; set; }
    public int StepOrder { get; set; }
    public Guid? ParentNodeId { get; set; }
    public string? TransmissionTerm { get; set; }
    public string? GradeEn { get; set; }
    public bool IsMudallis { get; set; }
    public bool HasMukhtalit { get; set; }
    public bool IsAnomaly { get; set; }
    public string? AnomalyReason { get; set; }
    public Guid SourceHadithId { get; set; }
    public string SourceBookName { get; set; } = string.Empty;
}
