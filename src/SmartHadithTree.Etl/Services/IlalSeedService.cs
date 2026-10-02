using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Domain.Utilities;
using SmartHadithTree.Etl.Parsers.Itqan;
using SmartHadithTree.Infrastructure.Data;

namespace SmartHadithTree.Etl.Services;

/// <summary>
/// Loads the data the Ilal engine depends on:
/// <list type="number">
/// <item>Teacher/student relations from the Itqan rijal profiles (اللقاء والسماع).</item>
/// <item>The curated mudallisin list (Ibn Hajr's tiers) from <c>Seeds/mudallisin.json</c>.</item>
/// <item>The curated mukhtalitun list with before/after students from <c>Seeds/mukhtalitun.json</c>.</item>
/// </list>
/// Safe to re-run: relations are replaced and flags are reset before being re-applied.
/// </summary>
public class IlalSeedService(
    HadithTreeDbContext context,
    ContextualDisambiguator disambiguator,
    BulkDataIngestionService bulkIngestion,
    ILogger<IlalSeedService> logger)
{
    private const string ItqanSource = "itqan";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record MudallisSeed(List<string> Names, int? ItqanId, int Tier);

    private sealed record MukhtalitSeed(
        List<string> Names,
        int? ItqanId,
        string? Note,
        List<List<string>> HeardBefore,
        List<List<string>> HeardAfter);

    private sealed record SeedFile<T>([property: JsonPropertyName("entries")] List<T> Entries);

    private sealed record NarratorLookup(Guid Id, int? ItqanId, string FullName, string? KnownAs, int Usage);

    public async Task RunAsync(string itqanSourcePath, string seedsPath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        await ImportRelationsAsync(itqanSourcePath, ct);

        var lookup = await LoadNarratorLookupAsync(ct);
        await ApplyMudallisinAsync(Path.Combine(seedsPath, "mudallisin.json"), lookup, ct);
        await ApplyMukhtalitunAsync(Path.Combine(seedsPath, "mukhtalitun.json"), lookup, ct);

        logger.LogInformation("Ilal seed completed in {Elapsed}.", sw.Elapsed);
    }

    // ── 1. Teacher/student relations ──────────────────────────────────

    private async Task ImportRelationsAsync(string itqanSourcePath, CancellationToken ct)
    {
        await disambiguator.InitializeAsync(itqanSourcePath, ct);
        if (disambiguator.Graph.Count == 0)
        {
            logger.LogWarning("No Itqan rijal graph found at {Path}; skipping relations.", itqanSourcePath);
            return;
        }

        var itqanToGuid = await context.Narrators.AsNoTracking()
            .Where(n => n.ItqanId != null)
            .Select(n => new { n.Id, ItqanId = n.ItqanId!.Value })
            .ToDictionaryAsync(n => n.ItqanId, n => n.Id, ct);

        // Union of both directions: a teacher's students and a student's teachers.
        var pairs = new HashSet<(Guid Teacher, Guid Student)>();
        foreach (var node in disambiguator.Graph.Values)
        {
            if (!itqanToGuid.TryGetValue(node.ItqanId, out var self)) continue;

            foreach (var t in node.Teachers)
                if (itqanToGuid.TryGetValue(t, out var teacher) && teacher != self)
                    pairs.Add((teacher, self));

            foreach (var s in node.Students)
                if (itqanToGuid.TryGetValue(s, out var student) && student != self)
                    pairs.Add((self, student));
        }

        var relations = pairs
            .Select(p => new NarratorRelation { Id = Guid.NewGuid(), TeacherId = p.Teacher, StudentId = p.Student, Source = ItqanSource })
            .ToList();

        await bulkIngestion.ReplaceNarratorRelationsAsync(ItqanSource, relations, ct);
    }

    // ── 2. Mudallisin ─────────────────────────────────────────────────

    private async Task ApplyMudallisinAsync(string path, IReadOnlyList<NarratorLookup> lookup, CancellationToken ct)
    {
        var seeds = await ReadSeedAsync<MudallisSeed>(path, ct);
        if (seeds.Count == 0) return;

        await context.Narrators.Where(n => n.IsMudallis || n.MudallisTier != null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsMudallis, false).SetProperty(n => n.MudallisTier, (int?)null), ct);

        var matched = 0;
        foreach (var seed in seeds)
        {
            var id = Resolve(lookup, seed.Names, seed.ItqanId);
            if (id == null)
            {
                logger.LogWarning("Mudallis not matched: {Name}", seed.Names[0]);
                continue;
            }

            await context.Narrators.Where(n => n.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsMudallis, true).SetProperty(n => n.MudallisTier, seed.Tier), ct);
            matched++;
        }

        logger.LogInformation("Mudallisin: matched {Matched}/{Total}.", matched, seeds.Count);
    }

    // ── 3. Mukhtalitun ────────────────────────────────────────────────

    private async Task ApplyMukhtalitunAsync(string path, IReadOnlyList<NarratorLookup> lookup, CancellationToken ct)
    {
        var seeds = await ReadSeedAsync<MukhtalitSeed>(path, ct);
        if (seeds.Count == 0) return;

        await context.Narrators.Where(n => n.HasMukhtalit || n.IkhtilatNote != null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.HasMukhtalit, false).SetProperty(n => n.IkhtilatNote, (string?)null), ct);
        await context.MukhtalitHearings.ExecuteDeleteAsync(ct);

        var matched = 0;
        var hearings = new List<MukhtalitHearing>();

        foreach (var seed in seeds)
        {
            var id = Resolve(lookup, seed.Names, seed.ItqanId);
            if (id == null)
            {
                logger.LogWarning("Mukhtalit not matched: {Name}", seed.Names[0]);
                continue;
            }

            await context.Narrators.Where(n => n.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.HasMukhtalit, true).SetProperty(n => n.IkhtilatNote, seed.Note), ct);
            matched++;

            AddHearings(seed.HeardBefore, HearingTiming.Before);
            AddHearings(seed.HeardAfter, HearingTiming.After);

            void AddHearings(IEnumerable<List<string>> students, HearingTiming timing)
            {
                foreach (var names in students)
                {
                    var studentId = Resolve(lookup, names, null);
                    if (studentId == null)
                    {
                        logger.LogWarning("Student of mukhtalit {Mukhtalit} not matched: {Name}", seed.Names[0], names[0]);
                        continue;
                    }
                    if (hearings.Any(h => h.MukhtalitId == id && h.StudentId == studentId)) continue;

                    hearings.Add(new MukhtalitHearing
                    {
                        Id = Guid.NewGuid(),
                        MukhtalitId = id.Value,
                        StudentId = studentId.Value,
                        Timing = timing
                    });
                }
            }
        }

        context.MukhtalitHearings.AddRange(hearings);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Mukhtalitun: matched {Matched}/{Total}, {Hearings} hearing records.",
            matched, seeds.Count, hearings.Count);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private async Task<List<T>> ReadSeedAsync<T>(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            logger.LogWarning("Seed file not found: {Path}", path);
            return [];
        }

        await using var stream = File.OpenRead(path);
        var file = await JsonSerializer.DeserializeAsync<SeedFile<T>>(stream, JsonOptions, ct);
        return file?.Entries ?? [];
    }

    private async Task<List<NarratorLookup>> LoadNarratorLookupAsync(CancellationToken ct)
    {
        // Usage (number of transmissions) breaks ties between narrators with the same name.
        var usage = await context.Transmissions.AsNoTracking()
            .GroupBy(t => t.SheikhId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        var narrators = await context.Narrators.AsNoTracking()
            .Select(n => new { n.Id, n.ItqanId, n.FullName, n.KnownAs })
            .ToListAsync(ct);

        return narrators
            .Select(n => new NarratorLookup(
                n.Id,
                n.ItqanId,
                MatnText.NormalizeForComparison(n.FullName),
                n.KnownAs == null ? null : MatnText.NormalizeForComparison(n.KnownAs),
                usage.GetValueOrDefault(n.Id)))
            .ToList();
    }

    /// <summary>
    /// Resolves a seed entry to a narrator: by Itqan ID, then exact name match, then (for names of
    /// three words or more) a full-name prefix match. Ties go to the most-used narrator.
    /// </summary>
    private static Guid? Resolve(IReadOnlyList<NarratorLookup> lookup, IReadOnlyList<string> names, int? itqanId)
    {
        if (itqanId.HasValue)
        {
            var byId = lookup.FirstOrDefault(n => n.ItqanId == itqanId);
            if (byId != null) return byId.Id;
        }

        foreach (var raw in names)
        {
            var name = MatnText.NormalizeForComparison(raw);
            if (name.Length == 0) continue;

            var exact = lookup.Where(n => n.FullName == name || n.KnownAs == name).ToList();
            if (exact.Count > 0) return exact.MaxBy(n => n.Usage)!.Id;

            if (name.Split(' ').Length >= 3)
            {
                var prefix = lookup.Where(n => n.FullName.StartsWith(name + " ", StringComparison.Ordinal)).ToList();
                if (prefix.Count > 0) return prefix.MaxBy(n => n.Usage)!.Id;
            }
        }

        return null;
    }
}
