using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Etl.Models;

namespace SmartHadithTree.Etl.Parsers.Itqan;

public class ContextualDisambiguator
{
    private readonly ILogger<ContextualDisambiguator>? _logger;
    private readonly Dictionary<int, NarratorNode> _graph = new();
    private readonly Dictionary<string, List<int>> _nameToCandidates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The loaded narrator graph keyed by Itqan ID (teachers/students per narrator).</summary>
    public IReadOnlyDictionary<int, NarratorNode> Graph => _graph;

    public ContextualDisambiguator(ILogger<ContextualDisambiguator>? logger = null)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(string sourcePath, CancellationToken ct = default)
    {
        var rijalDir = Path.Combine(sourcePath, "rijal");
        if (!Directory.Exists(rijalDir))
        {
            _logger?.LogWarning("Rijal directory not found at {Path}. Disambiguator will be empty.", rijalDir);
            return;
        }

        _logger?.LogInformation("Initializing Contextual Disambiguator Engine...");
        
        // 1. Load the Graph (profiles_*.json)
        var profileFiles = Directory.GetFiles(rijalDir, "profiles_*.json");
        foreach (var file in profileFiles)
        {
            await using var stream = File.OpenRead(file);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            
            foreach (var element in doc.RootElement.EnumerateObject())
            {
                var profile = element.Value;
                if (!profile.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.Number)
                    continue;

                var id = idProp.GetInt32();
                var node = new NarratorNode
                {
                    ItqanId = id,
                    FullName = profile.TryGetProperty("full_name", out var fnProp) ? fnProp.GetString() ?? "" : "",
                    Generation = profile.TryGetProperty("generation", out var gProp) && gProp.ValueKind == JsonValueKind.Number ? gProp.GetInt32() : null,
                    IdScore = profile.TryGetProperty("id_score", out var idsProp) && idsProp.ValueKind == JsonValueKind.Number ? idsProp.GetInt32() : 0,
                    GradeScore = profile.TryGetProperty("grade_score", out var gsProp) && gsProp.ValueKind == JsonValueKind.Number ? gsProp.GetInt32() : 0,
                };

                if (profile.TryGetProperty("teachers", out var teachersProp) && teachersProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var t in teachersProp.EnumerateArray())
                    {
                        if (t.ValueKind == JsonValueKind.Number) node.Teachers.Add(t.GetInt32());
                    }
                }

                if (profile.TryGetProperty("students", out var studentsProp) && studentsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in studentsProp.EnumerateArray())
                    {
                        if (s.ValueKind == JsonValueKind.Number) node.Students.Add(s.GetInt32());
                    }
                }

                _graph[id] = node;
            }
        }

        // 2. Load Names to Candidates (by_name.json)
        var byNameFile = Path.Combine(rijalDir, "by_name.json");
        if (File.Exists(byNameFile))
        {
            await using var stream = File.OpenRead(byNameFile);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            foreach (var element in doc.RootElement.EnumerateObject())
            {
                var candidates = new List<int>();
                if (element.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in element.Value.EnumerateArray())
                    {
                        if (c.ValueKind == JsonValueKind.String && int.TryParse(c.GetString(), out var cId))
                        {
                            candidates.Add(cId);
                        }
                        else if (c.ValueKind == JsonValueKind.Number)
                        {
                            candidates.Add(c.GetInt32());
                        }
                    }
                }
                
                if (candidates.Count > 0)
                {
                    _nameToCandidates[element.Name] = candidates;
                }
            }
        }

        _logger?.LogInformation("Engine Initialized: Loaded {NodeCount} nodes and {NameCount} names.", _graph.Count, _nameToCandidates.Count);
    }

    /// <summary>
    /// Resolves an ambiguous narrator name to a specific Itqan ID using graph context (Teacher-Student relationships).
    /// </summary>
    public int? ResolveSheikh(string rawName, int? studentItqanId)
    {
        var explicitOverride = GetContextualOverride(rawName, studentItqanId);
        _nameToCandidates.TryGetValue(rawName, out var candidates);

        if (explicitOverride.HasValue &&
            ((candidates != null && candidates.Contains(explicitOverride.Value)) || _graph.ContainsKey(explicitOverride.Value)))
        {
            return explicitOverride.Value;
        }

        if (candidates == null || candidates.Count == 0)
        {
            return null; // Name not found
        }

        // If only 1 candidate, return it instantly
        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        // If we have a student ID to contextualize with
        if (studentItqanId.HasValue)
        {
            var studentId = studentItqanId.Value;
            var graphMatches = new List<NarratorNode>();

            foreach (var cId in candidates)
            {
                if (_graph.TryGetValue(cId, out var candidateNode))
                {
                    bool candidateHasStudent = candidateNode.Students.Contains(studentId);
                    bool studentHasCandidate = false;
                    
                    if (_graph.TryGetValue(studentId, out var studentNode))
                    {
                        studentHasCandidate = studentNode.Teachers.Contains(cId);
                    }

                    if (candidateHasStudent || studentHasCandidate)
                    {
                        graphMatches.Add(candidateNode);
                    }
                }
            }

            if (graphMatches.Count == 1)
            {
                return graphMatches[0].ItqanId;
            }
            else if (graphMatches.Count > 1)
            {
                // Tie-breaker amongst graph matches
                return ResolveTieBreaker(rawName, graphMatches, studentItqanId);
            }
        }

        // Fallback: No student context or no graph matches found
        // Use generic static override if exists
        var staticOverride = GetContextualOverride(rawName, null);
        if (staticOverride.HasValue && (candidates.Contains(staticOverride.Value) || _graph.ContainsKey(staticOverride.Value)))
        {
            return staticOverride.Value;
        }

        // Load candidates to resolve tie breaker
        var candidateNodes = candidates.Select(cId => _graph.TryGetValue(cId, out var n) ? n : null)
                                       .Where(n => n != null)
                                       .Select(n => n!)
                                       .ToList();

        if (candidateNodes.Count > 0)
        {
             return ResolveTieBreaker(rawName, candidateNodes, studentItqanId);
        }

        return candidates[0]; // Absolute fallback to the first element
    }

    private int? GetContextualOverride(string rawName, int? studentItqanId)
    {
        if (rawName.Equals("أبي", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("ابي", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("أبيه", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("ابيه", StringComparison.OrdinalIgnoreCase))
        {
            if (studentItqanId == 333) // عبد الله بن أحمد بن حنبل عن أبيه
                return 353; // أحمد بن محمد بن حنبل
        }

        if (rawName.Equals("أبو سلمة", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("أبي سلمة", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("أبو سلمة بن عبد الرحمن", StringComparison.OrdinalIgnoreCase))
        {
            return 303; // أبو سلمة بن عبد الرحمن بن عوف الزهري (التابعي الجليل، لا الصحابي 59420)
        }

        if (rawName.Equals("محمد بن عمرو", StringComparison.OrdinalIgnoreCase))
        {
            return 17; // محمد بن عمرو بن علقمة بن وقاص الليثي (المدار المشهور عن أبي سلمة)
        }

        if (rawName.Equals("محمد بن عبيد", StringComparison.OrdinalIgnoreCase))
        {
            return 2271; // محمد بن عبيد بن أبي أمية الطنافسي (أخو يعلى بن عبيد 502)
        }

        if (rawName.Equals("يعلى بن عبيد", StringComparison.OrdinalIgnoreCase))
        {
            return 502; // يعلى بن عبيد بن أبي أمية الطنافسي
        }

        if (rawName.Equals("أبو بكر بن أبي شيبة", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("أبو بكر بن أبو شيبة", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("ابن أبي شيبة", StringComparison.OrdinalIgnoreCase))
        {
            return 748; // عبد الله بن محمد بن أبي شيبة
        }

        if (rawName.Equals("إسماعيل بن علية", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("إسماعيل ابن علية", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("ابن علية", StringComparison.OrdinalIgnoreCase))
        {
            return 360; // إسماعيل بن إبراهيم بن مقسم الأسدي (ابن علية)
        }

        if (rawName.Equals("أبو عبد الله الحافظ", StringComparison.OrdinalIgnoreCase))
        {
            return 10; // محمد بن عبد الله الحاكم النيسابوري
        }

        if (rawName.Equals("أبو العباس محمد بن يعقوب", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("أبو العباس الأصم", StringComparison.OrdinalIgnoreCase))
        {
            return 842; // محمد بن يعقوب بن يوسف بن معقل بن سنان الأصم النيسابوري
        }

        if (rawName.Equals("محمد بن يعقوب", StringComparison.OrdinalIgnoreCase) &&
            (studentItqanId == 10 || studentItqanId == 34))
        {
            return 842; // أبو العباس الأصم (شيخ الحاكم والبيهقي بالواسطة)
        }

        if (rawName.Equals("حجاج بن إبراهيم الأزرق", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("حجاج بن إبراهيم", StringComparison.OrdinalIgnoreCase))
        {
            return 4393; // حجاج بن إبراهيم الأزرق البغدادي المصري
        }

        if (rawName.Equals("معمر", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("معمر بن راشد", StringComparison.OrdinalIgnoreCase))
        {
            return 40; // معمر بن راشد الأزدي (شيخ عبد الرزاق وغيره)
        }

        if (rawName.Equals("ابن جريج", StringComparison.OrdinalIgnoreCase))
        {
            return 110; // عبد الملك بن عبد العزيز بن جريج
        }

        if (rawName.Equals("ابن عيينة", StringComparison.OrdinalIgnoreCase))
        {
            return 192; // سفيان بن عيينة
        }

        if (rawName.Equals("الثوري", StringComparison.OrdinalIgnoreCase))
        {
            return 434; // سفيان بن سعيد الثوري
        }

        if (rawName.Equals("سفيان", StringComparison.OrdinalIgnoreCase))
        {
            if (studentItqanId.HasValue)
            {
                // Students famous for narrating from Sufyan al-Thawri (434) — including Abd al-Razzaq (44)
                var thawriStudents = new HashSet<int> { 44, 1191, 669, 2345, 2588, 1284, 1874, 5343, 7643, 7704, 2853, 2055 };
                if (thawriStudents.Contains(studentItqanId.Value)) return 434;

                // Students famous for narrating from Sufyan ibn Uyaynah (192) — including al-Shafi'i (2734), al-Humaydi (82), Sa'id ibn Mansur (1959)
                var uyaynahStudents = new HashSet<int> { 2734, 82, 1959, 1453, 4557, 617, 1218, 673, 54, 532, 1690, 74, 55562, 618 };
                if (uyaynahStudents.Contains(studentItqanId.Value)) return 192;
            }
            return 192; // Default generic fallback for Sufyan
        }

        if (rawName.Equals("محمد بن كثير", StringComparison.OrdinalIgnoreCase))
        {
            if (studentItqanId == 74 || studentItqanId == 55562 || studentItqanId == 618)
            {
                return 1191; // Al-Abdi
            }
            return 1191;
        }
        
        if (rawName.Equals("يحيى بن سعيد", StringComparison.OrdinalIgnoreCase))
        {
            return 199; // يحيى بن سعيد الأنصاري
        }

        if (rawName.Equals("شقيق", StringComparison.OrdinalIgnoreCase))
        {
            return 250; // شقيق بن سلمة (أبو وائل)
        }

        if (rawName.Equals("ابن نمير", StringComparison.OrdinalIgnoreCase))
        {
            return 818; // عبد الله بن نمير
        }

        if (rawName.Equals("عبد الله", StringComparison.OrdinalIgnoreCase))
        {
            if (studentItqanId == 250) // شقيق بن سلمة يروي عن عبد الله بن مسعود
            {
                return 529; // عبد الله بن مسعود
            }
        }

        return null;
    }

    private int ResolveTieBreaker(string rawName, List<NarratorNode> candidates, int? studentItqanId)
    {
        var contextualOverride = GetContextualOverride(rawName, studentItqanId);
        if (contextualOverride.HasValue && candidates.Any(c => c.ItqanId == contextualOverride.Value))
        {
            return contextualOverride.Value;
        }

        // Prefer candidates whose FullName actually begins with the queried rawName
        var prefixMatches = candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.FullName) &&
                        c.FullName.StartsWith(rawName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var pool = prefixMatches.Count > 0 ? prefixMatches : candidates;

        // Highest ID Score (Prominence in Itqan dataset), then GradeScore
        var ordered = pool.OrderByDescending(c => c.IdScore).ThenByDescending(c => c.GradeScore).ToList();
        return ordered.First().ItqanId;
    }
}
