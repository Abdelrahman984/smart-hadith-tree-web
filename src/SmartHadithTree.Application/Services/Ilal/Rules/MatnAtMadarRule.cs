using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services.Ilal.Rules;

/// <summary>
/// Compares the matn carried by each branch at every madar to detect:
/// زيادة الثقة (accepted addition), الشذوذ (a reliable narrator contradicting stronger/more numerous peers),
/// النكارة (a weak narrator contradicting reliable peers), and الاضطراب (conflict between
/// branches of comparable strength that cannot be resolved by preference).
/// </summary>
public sealed class MatnAtMadarRule : IIlalRule
{
    /// <summary>Below this similarity the texts are treated as different hadiths and not compared.</summary>
    public const double MinRelatedSimilarity = 0.4;

    /// <summary>Minimum number of words for an addition or omission to count.</summary>
    public const int MinSignificantWords = 3;

    /// <summary>
    /// Minimum number of substituted words for a contradiction. Single-word substitutions are
    /// usually narration by meaning (الرواية بالمعنى) and are not flagged.
    /// </summary>
    public const int MinSubstitutedWords = 2;

    /// <summary>
    /// A text with at most this many content words, wholly contained in a longer one, is an abridgement
    /// (a compiler quoting only the opening saying), not an omission by the narrator. A full narration of
    /// its own is longer (the shortest one compared in the tests has four).
    /// </summary>
    public const int MaxFragmentWords = 3;

    /// <summary>How a compared text differs from a reference text.</summary>
    public enum DiffKind { None, Unrelated, Addition, Omission, Contradiction }

    private enum Fragment { None, Reference, Compared }

    /// <summary>One branch's text at the madar, with its place in the fixed order the clusters are built in.</summary>
    private sealed record Text(IsnadBranch Branch, IlalChain Chain, string[] Tokens, int Order);

    private sealed class TextCluster
    {
        public List<Text> Members { get; } = [];
        public List<IsnadBranch> Branches => Members.Select(m => m.Branch).ToList();
        public int BestTier => Branches.Min(b => b.StudentTier);

        /// <summary>
        /// Every student is graded and the best of them is weak (rank 6 or below). A student without a grade
        /// is «غير محرر», no verdict, so a side that has one is not called weak.
        /// </summary>
        public bool KnownWeak => Branches.All(b => b.StudentRanked) && BestTier >= 6;

        public bool HasUnranked => Branches.Any(b => !b.StudentRanked);
    }

    public IEnumerable<IlalFindingDto> Evaluate(IlalContext context)
    {
        foreach (var split in IsnadBranching.FindSplitPoints(context))
        {
            foreach (var finding in EvaluateSplit(context, split))
                yield return finding;
        }
    }

    private static IEnumerable<IlalFindingDto> EvaluateSplit(IlalContext context, SplitPoint split)
    {
        // 1. Group branches whose texts agree (only minor wording differences) into clusters.
        var tokensOf = split.AllChains.Distinct()
            .ToDictionary(c => c, c => MatnText.Tokenize(MatnText.ExtractBody(c.MatnArabic)));
        var candidates = split.Branches
            .Select(b => (Branch: b, Rep: RepresentativeOf(b, tokensOf)))
            .Where(c => c.Rep.Tokens.Length > 0)
            .ToList();

        // An abridged text says nothing about the part it leaves out, so it is not evidence for any cluster.
        var kept = candidates
            .Where(c => !candidates.Any(o => o.Branch != c.Branch
                && FindFragment(MatnAligner.Align(o.Rep.Tokens, c.Rep.Tokens)) == Fragment.Compared))
            .ToList();

        // Nothing below depends on the order the turuq came in: taking the first text of a cluster as its reference made
        // the same turuq give different verdicts (مسند أحمد #2 against أبي يعلى #12 or ابن ماجه #1395, whichever came
        // first). The texts get a fixed order, most typical first (highest total similarity to the texts of the other
        // records; two branches of one tahwil record carry one text), then the stronger student, then the shorter text,
        // then the text itself; it decides only ties.
        var typicality = kept.Select(c => kept
                .Where(o => o.Rep.Chain.HadithId != c.Rep.Chain.HadithId)
                .Sum(o => MatnAligner.Align(c.Rep.Tokens, o.Rep.Tokens).Similarity))
            .ToList();
        var texts = kept
            .Select((c, i) => (c.Branch, c.Rep.Chain, c.Rep.Tokens, Typicality: Math.Round(typicality[i], 9)))
            .OrderByDescending(c => c.Typicality)
            .ThenBy(c => c.Branch.StudentTier)
            .ThenBy(c => c.Tokens.Length)
            .ThenBy(c => string.Join(' ', c.Tokens), StringComparer.Ordinal)
            .ThenBy(c => c.Branch.StudentId)
            .Select((c, i) => new Text(c.Branch, c.Chain, c.Tokens, i))
            .ToList();

        // Two texts that agree are one version, and so are texts linked through others that agree: «agrees» is not
        // transitive near the thresholds, and a cluster grown from one seed text changed with the seed.
        var clusterOf = Enumerable.Range(0, texts.Count).ToArray();
        int Root(int i) => clusterOf[i] == i ? i : clusterOf[i] = Root(clusterOf[i]);
        for (var i = 0; i < texts.Count; i++)
            for (var j = i + 1; j < texts.Count; j++)
                if (Classify(MatnAligner.Align(texts[i].Tokens, texts[j].Tokens)) == DiffKind.None
                    || Classify(MatnAligner.Align(texts[j].Tokens, texts[i].Tokens)) == DiffKind.None)
                    clusterOf[Math.Max(Root(i), Root(j))] = Math.Min(Root(i), Root(j));

        var clusters = texts
            .GroupBy(t => Root(t.Order))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var cluster = new TextCluster();
                cluster.Members.AddRange(g);
                return cluster;
            })
            .ToList();

        if (clusters.Count < 2) yield break;

        // 2. The preferred text (المحفوظ) is carried by the strongest cluster.
        var main = clusters.Aggregate((best, c) => IsnadBranching.Compare(c.Branches, best.Branches) > 0 ? c : best);
        var madarName = context.NameOf(split.MadarId);
        var conflicting = new List<(TextCluster Cluster, Text Reference, Text Compared, AlignmentResult Diff)>();

        foreach (var other in clusters.Where(c => c != main))
        {
            // Each side is read by its text closest to the other side, so a difference is reported only when even the
            // nearest readings of the two versions differ.
            var (reference, compared, diff) = ClosestPair(main, other);
            var kind = Classify(diff);
            if (kind is DiffKind.None or DiffKind.Unrelated) continue;

            var comparison = ToComparison(reference.Chain, compared.Chain, diff);

            if (kind == DiffKind.Contradiction)
            {
                if (IsnadBranching.Compare(main.Branches, other.Branches) == 0)
                {
                    conflicting.Add((other, reference, compared, diff));
                    continue;
                }

                var weak = other.KnownWeak;
                if (!weak && other.HasUnranked)
                {
                    yield return new IlalFindingDto
                    {
                        Type = IllahType.Shudhudh,
                        Severity = IllahSeverity.Tanbih,
                        TitleAr = "مخالفة في المتن، وراويها غير محرر",
                        EvidenceAr =
                            $"اختلف الرواة عن {madarName} في لفظ المتن: رواه {Describe(context, other)} بلفظ يخالف رواية {Describe(context, main)}، "
                            + "ولم يُحرَّر حال راويها في الكتب المعتمدة، فلا يُحكم على روايته بالنكارة ولا بالشذوذ حتى يُنظر في حاله.",
                        NarratorIds = [split.MadarId, .. other.Branches.Select(b => b.StudentId)],
                        HadithIds = other.Branches.SelectMany(b => b.Chains).Select(c => c.HadithId).Distinct().ToList(),
                        Confidence = 0.35,
                        MatnComparison = comparison
                    };
                    continue;
                }

                yield return new IlalFindingDto
                {
                    Type = weak ? IllahType.Nakarah : IllahType.Shudhudh,
                    Severity = IllahSeverity.Qadihah,
                    TitleAr = weak ? "مخالفة منكرة في المتن" : "مخالفة شاذة في المتن",
                    EvidenceAr =
                        $"اختلف الرواة عن {madarName} في لفظ المتن: رواه {Describe(context, other)} بلفظ يخالف رواية {Describe(context, main)}، "
                        + $"وهم أرجح {(main.Branches.Count > other.Branches.Count ? "عدداً" : "حفظاً")}، فالمحفوظ روايتهم"
                        + (weak ? " والمخالفة منكرة لضعف راويها." : " ورواية المخالف شاذة."),
                    NarratorIds = [split.MadarId, .. other.Branches.Select(b => b.StudentId)],
                    HadithIds = other.Branches.SelectMany(b => b.Chains).Select(c => c.HadithId).Distinct().ToList(),
                    Confidence = 0.5,
                    MatnComparison = comparison
                };
                continue;
            }

            // Addition by one side (زيادة)
            var (adder, omitter) = kind == DiffKind.Addition ? (other, main) : (main, other);
            var addedText = string.Join(" / ", diff.Segments
                .Where(s => s.Kind == (kind == DiffKind.Addition ? AlignmentKind.Added : AlignmentKind.Removed) && s.Tokens.Count >= 2)
                .Select(s => s.Text));
            var baseEvidence =
                $"زاد {Describe(context, adder)} عن {madarName} في المتن: «{addedText}»، ولم يذكرها {Describe(context, omitter)}.";

            if (adder.KnownWeak)
            {
                yield return Addition(IllahType.Nakarah, IllahSeverity.Qadihah, "زيادة منكرة",
                    $"{baseEvidence} وراوي الزيادة ضعيف فلا تقبل زيادته.", 0.6);
            }
            else if (IsnadBranching.Compare(adder.Branches, omitter.Branches) < 0)
            {
                yield return Addition(IllahType.Shudhudh, IllahSeverity.Tanbih, "زيادة يُخشى شذوذها",
                    $"{baseEvidence} ومن لم يذكرها أرجح، فيُنظر في قرائن قبول الزيادة.", 0.45);
            }
            else
            {
                var trusted = adder.BestTier <= 4 && !adder.HasUnranked;
                yield return Addition(IllahType.Ziyadah, trusted ? IllahSeverity.GhayrQadihah : IllahSeverity.Tanbih,
                    trusted ? "زيادة ثقة" : "زيادة تحتاج إلى نظر",
                    trusted
                        ? $"{baseEvidence} وراويها ثقة لم يخالف من هو أرجح منه، فهي زيادة ثقة مقبولة."
                        : adder.HasUnranked
                            ? $"{baseEvidence} وراويها غير محرر الحال في الكتب المعتمدة، فيُنظر في قبولها."
                            : $"{baseEvidence} وراويها دون الثقة، فيُنظر في قبولها.",
                    trusted ? 0.6 : 0.45);
            }

            IlalFindingDto Addition(IllahType type, IllahSeverity severity, string title, string evidence, double confidence) => new()
            {
                Type = type,
                Severity = severity,
                TitleAr = title,
                EvidenceAr = evidence,
                NarratorIds = [split.MadarId, .. adder.Branches.Select(b => b.StudentId)],
                HadithIds = adder.Branches.SelectMany(b => b.Chains).Select(c => c.HadithId).Distinct().ToList(),
                Confidence = confidence,
                MatnComparison = comparison
            };
        }

        // 3. Conflicts that cannot be resolved by preference: اضطراب
        if (conflicting.Count > 0)
        {
            var involved = conflicting.Select(c => c.Cluster).Prepend(main).ToList();
            var first = conflicting[0];
            yield return new IlalFindingDto
            {
                Type = IllahType.Idtirab,
                Severity = IllahSeverity.Qadihah,
                TitleAr = "اضطراب في المتن",
                EvidenceAr =
                    $"اختلف الرواة عن {madarName} في المتن اختلافاً لا يمكن الترجيح فيه لتقارب منزلتهم: "
                    + string.Join("، ", involved.Select(c => $"رواية {Describe(context, c)}"))
                    + ". فإن تعذّر الجمع بين الروايات فالحديث مضطرب.",
                NarratorIds = [split.MadarId, .. involved.SelectMany(c => c.Branches).Select(b => b.StudentId)],
                HadithIds = involved.SelectMany(c => c.Branches).SelectMany(b => b.Chains).Select(c => c.HadithId).Distinct().ToList(),
                Confidence = 0.45,
                MatnComparison = ToComparison(first.Reference.Chain, first.Compared.Chain, first.Diff)
            };
        }
    }

    /// <summary>
    /// The text of <paramref name="main"/> and the text of <paramref name="other"/> most similar to each other; ties go
    /// to the earlier texts of the fixed order.
    /// </summary>
    private static (Text Reference, Text Compared, AlignmentResult Diff) ClosestPair(TextCluster main, TextCluster other) =>
        main.Members
            .SelectMany(m => other.Members.Select(o => (Reference: m, Compared: o, Diff: MatnAligner.Align(m.Tokens, o.Tokens))))
            .OrderByDescending(p => Math.Round(p.Diff.Similarity, 9))
            .ThenBy(p => p.Reference.Order)
            .ThenBy(p => p.Compared.Order)
            .First();

    /// <summary>
    /// The text a branch is compared by when several books carry the same student's narration, never simply the first
    /// listed: the one most similar to the texts of the other records at the madar (a compiler's notes or an abridgement
    /// make a text less typical), then the longest, then a fixed order.
    /// </summary>
    private static (IlalChain Chain, string[] Tokens) RepresentativeOf(
        IsnadBranch branch, IReadOnlyDictionary<IlalChain, string[]> tokensOf)
    {
        if (branch.Chains.Count == 1) return (branch.Chains[0], tokensOf[branch.Chains[0]]);

        return branch.Chains
            .Select(c => (Chain: c, Tokens: tokensOf[c]))
            .Select(t => (Text: t, Total: t.Tokens.Length == 0 ? 0 : Math.Round(tokensOf
                .Where(o => o.Key.HadithId != t.Chain.HadithId && o.Value.Length > 0)
                .DistinctBy(o => o.Key.HadithId)
                .Sum(o => MatnAligner.Align(t.Tokens, o.Value).Similarity), 9)))
            .OrderByDescending(x => x.Total)
            .ThenByDescending(x => x.Text.Tokens.Length)
            .ThenBy(x => string.Join(' ', x.Text.Tokens), StringComparer.Ordinal)
            .ThenBy(x => x.Text.Chain.HadithId)
            .First().Text;
    }

    private static readonly HashSet<string> FramingTokens = new(StringComparer.Ordinal)
    {
        "ان", "انه", "انها", "قال", "فقال", "يقول", "النبي", "رسول", "الله", "نبي",
        // Narrative preface that only sets the scene («كنت مع رسول الله في بعض أسفاره وكان …»); a text
        // that opens with it and one that does not are the same hadith, not an addition.
        "كنت", "مع", "في", "بعض", "اسفاره", "كان", "وكان"
    };

    /// <summary>Classifies the difference between a reference text and a compared text.</summary>
    public static DiffKind Classify(AlignmentResult diff)
    {
        if (!AreRelated(diff)) return DiffKind.Unrelated;
        if (FindFragment(diff) != Fragment.None) return DiffKind.None;

        var addedTokens = diff.Segments
            .Where(s => s.Kind == AlignmentKind.Added)
            .SelectMany(s => s.Tokens)
            .Where(t => !FramingTokens.Contains(t))
            .ToList();
        var removedTokens = diff.Segments
            .Where(s => s.Kind == AlignmentKind.Removed)
            .SelectMany(s => s.Tokens)
            .Where(t => !FramingTokens.Contains(t))
            .ToList();

        // Discount word-order transpositions (تقديم وتأخير) where the exact same word appears in both Added and Removed
        var removedSet = new HashSet<string>(removedTokens, StringComparer.Ordinal);
        int transposedCount = addedTokens.Count(removedSet.Contains);

        var sites = SubstitutionSites(diff);
        var substituted = sites.Sum();
        var maxSingleSubstitution = sites.DefaultIfEmpty(0).Max();
        var addedOnly = Math.Max(0, addedTokens.Count - substituted - transposedCount);
        var removedOnly = Math.Max(0, removedTokens.Count - substituted - transposedCount);

        if (addedOnly >= MinSignificantWords && removedOnly >= MinSignificantWords
            && LongestExcessRun(diff, AlignmentKind.Added) >= MinSignificantWords
            && LongestExcessRun(diff, AlignmentKind.Removed) >= MinSignificantWords)
        {
            return DiffKind.Contradiction;
        }
        // An addition or an omission is one stretch of text, not scattered words: a repeated phrase that the
        // alignment pairs with the wrong occurrence, or many small wording changes, add up to words but not to a stretch.
        if ((addedOnly >= MinSignificantWords || (addedTokens.Count - transposedCount >= MinSignificantWords && maxSingleSubstitution < MinSubstitutedWords))
            && LongestExcessRun(diff, AlignmentKind.Added) >= MinSignificantWords && removedOnly < MinSignificantWords)
        {
            return DiffKind.Addition;
        }
        if ((removedOnly >= MinSignificantWords || (removedTokens.Count - transposedCount >= MinSignificantWords && maxSingleSubstitution < MinSubstitutedWords))
            && LongestExcessRun(diff, AlignmentKind.Removed) >= MinSignificantWords && addedOnly < MinSignificantWords)
        {
            return DiffKind.Omission;
        }
        // Multi-word contiguous substitution at a single site is a direct contradiction. It comes after the
        // addition and omission checks: a text that lacks a whole stretch is an omission even when two adjacent
        // words elsewhere differ (a synonym next to a misprint).
        if (maxSingleSubstitution >= MinSubstitutedWords && diff.Similarity < 0.9) return DiffKind.Contradiction;
        // Single words swapped here and there (خشبه / خشبته, ضرار / إضرار) are narration by meaning or spelling,
        // the same as one swapped word, which is not flagged either; only a multi-word site (above) is.
        return DiffKind.None;
    }

    /// <summary>
    /// Whether the two texts are versions of the same matn. The overall similarity is not enough on its
    /// own: a long addition lowers it although the shorter text is wholly contained in the longer one,
    /// and framing words («كان النبي» / «كنت مع رسول الله في بعض أسفاره») inflate the difference in short texts.
    /// So the content words are compared too, by overlap with the shorter text.
    /// </summary>
    private static bool AreRelated(AlignmentResult diff)
    {
        if (diff.Similarity >= MinRelatedSimilarity) return true;

        var shared = diff.Segments.Where(s => s.Kind == AlignmentKind.Equal).Sum(s => s.Tokens.Count(IsContentWord));
        var reference = shared + diff.Segments.Where(s => s.Kind == AlignmentKind.Removed).Sum(s => s.Tokens.Count(IsContentWord));
        var compared = shared + diff.Segments.Where(s => s.Kind == AlignmentKind.Added).Sum(s => s.Tokens.Count(IsContentWord));
        var shorter = Math.Min(reference, compared);

        return shorter >= MinSignificantWords && (double)shared / shorter >= MinRelatedSimilarity;
    }

    /// <summary>
    /// Which side, if any, is only an abridgement of the other: it has no content word of its own and
    /// at most <see cref="MaxFragmentWords"/> in all.
    /// </summary>
    private static Fragment FindFragment(AlignmentResult diff)
    {
        var shared = diff.Segments.Where(s => s.Kind == AlignmentKind.Equal).Sum(s => s.Tokens.Count(IsContentWord));
        if (shared == 0 || shared > MaxFragmentWords) return Fragment.None;

        var referenceOwn = diff.Segments.Where(s => s.Kind == AlignmentKind.Removed).Sum(s => s.Tokens.Count(IsContentWord));
        var comparedOwn = diff.Segments.Where(s => s.Kind == AlignmentKind.Added).Sum(s => s.Tokens.Count(IsContentWord));

        if (referenceOwn == 0 && comparedOwn > 0) return Fragment.Reference;
        if (comparedOwn == 0 && referenceOwn > 0) return Fragment.Compared;
        return Fragment.None;
    }

    /// <summary>
    /// Size of each substitution site (a removed run directly next to an added run), counting only
    /// content words: swapping «كان النبي» for «كنت مع رسول الله» replaces framing, not wording.
    /// </summary>
    private static List<int> SubstitutionSites(AlignmentResult diff)
    {
        var sites = new List<int>();
        for (int i = 0; i < diff.Segments.Count - 1; i++)
        {
            var a = diff.Segments[i];
            var b = diff.Segments[i + 1];
            if (a.Kind != AlignmentKind.Equal && b.Kind != AlignmentKind.Equal && a.Kind != b.Kind)
            {
                var size = Math.Min(a.Tokens.Count(IsContentWord), b.Tokens.Count(IsContentWord));
                if (size > 0) sites.Add(size);
            }
        }
        return sites;
    }

    private static bool IsContentWord(string token) => !FramingTokens.Contains(token);

    /// <summary>
    /// The longest run of content words only one side has, beyond what the run next to it (the other side's words
    /// at the same place) replaces. A run that has two words or more opposite it is a substitution and counts as none.
    /// </summary>
    private static int LongestExcessRun(AlignmentResult diff, AlignmentKind kind)
    {
        var best = 0;
        for (var i = 0; i < diff.Segments.Count; i++)
        {
            var segment = diff.Segments[i];
            if (segment.Kind != kind) continue;

            var paired = 0;
            if (i > 0 && diff.Segments[i - 1].Kind is not (AlignmentKind.Equal) && diff.Segments[i - 1].Kind != kind)
                paired = Math.Max(paired, diff.Segments[i - 1].Tokens.Count(IsContentWord));
            if (i + 1 < diff.Segments.Count && diff.Segments[i + 1].Kind is not (AlignmentKind.Equal) && diff.Segments[i + 1].Kind != kind)
                paired = Math.Max(paired, diff.Segments[i + 1].Tokens.Count(IsContentWord));

            // Several words swapped for several others is a substitution (a contradiction, decided by its site);
            // only a run with little or nothing opposite it is text one side has and the other lacks.
            if (paired >= MinSubstitutedWords) continue;
            best = Math.Max(best, segment.Tokens.Count(IsContentWord) - paired);
        }
        return best;
    }

    private static string Describe(IlalContext context, TextCluster cluster) =>
        string.Join(" و", cluster.Branches.Select(b =>
            $"{context.NameOf(b.StudentId)} ({context.Narrator(b.StudentId)?.GradeLabel ?? "غير محرر"})"));

    private static MatnComparisonDto ToComparison(IlalChain reference, IlalChain compared, AlignmentResult diff) => new()
    {
        ReferenceHadithId = reference.HadithId,
        ComparedHadithId = compared.HadithId,
        Similarity = Math.Round(diff.Similarity, 3),
        Segments = diff.Segments.Select(s => new MatnSegmentDto
        {
            Kind = s.Kind switch
            {
                AlignmentKind.Added => "added",
                AlignmentKind.Removed => "removed",
                _ => "equal"
            },
            Text = s.Text
        }).ToList()
    };
}
