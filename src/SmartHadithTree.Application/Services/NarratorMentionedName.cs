using System.Text.RegularExpressions;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services;

/// <summary>
/// The same narrator is written differently in different isnads: the registry holds his full name, while the
/// books mention him as "سفيان", "ابن عيينة" or "الزهري". Given the isnad texts the user picked, this finds the form
/// of his own name that those texts use most often, so the tree can show the name the reader sees in the matns.
/// </summary>
public static partial class NarratorMentionedName
{
    private static readonly HashSet<string> Connectors = ["بن", "ابن", "بنت"];
    private static readonly HashSet<string> CompoundFirstWords = ["عبد", "ابي", "ابو", "ام"];

    /// <summary>
    /// The form of the narrator's name most often found in <paramref name="isnadTexts"/>, spelled as in his registry
    /// name; null when no form of it is found (the caller keeps its own display name).
    /// </summary>
    public static string? Choose(string? fullName, string? knownAs, IEnumerable<string?> isnadTexts)
    {
        var candidates = Candidates(fullName, knownAs);
        if (candidates.Count == 0) return null;

        var texts = isnadTexts
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => Tokenize(t!))
            .ToList();
        if (texts.Count == 0) return null;

        string? best = null;
        var bestCount = 0;
        var bestLength = 0;
        foreach (var (display, tokens) in candidates)
        {
            var count = texts.Sum(text => CountOccurrences(text, tokens));
            if (count == 0) continue;
            if (count > bestCount || (count == bestCount && tokens.Length > bestLength))
            {
                best = display;
                bestCount = count;
                bestLength = tokens.Length;
            }
        }

        return best;
    }

    /// <summary>Forms of the name worth looking for, each with its display spelling and normalized tokens.</summary>
    private static List<(string Display, string[] Tokens)> Candidates(string? fullName, string? knownAs)
    {
        var forms = new List<string>();

        var name = Clean(fullName);
        if (name.Length > 0)
        {
            var parts = PatronymicSplit().Split(name);
            // Split keeps the connectors: [segment, connector, segment, connector, segment ...]
            var segments = parts.Where((_, i) => i % 2 == 0).Select(s => s.Trim()).ToList();
            var connectors = parts.Where((_, i) => i % 2 == 1).Select(c => c.Trim()).ToList();

            var first = segments[0];
            forms.Add(first);

            if (segments.Count > 1)
            {
                var father = FirstName(segments[1]);
                forms.Add($"{first} {connectors[0]} {father}");
                if (connectors[0] != "بنت") forms.Add($"ابن {father}");

                if (segments.Count > 2)
                    forms.Add($"{first} {connectors[0]} {father} {connectors[1]} {FirstName(segments[2])}");

                // The nisbah closing the name ("الثوري"): alone, and after the first name ("سفيان الثوري").
                foreach (var nisbah in segments[^1].Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)
                             .Where(w => w.Length > 3 && w.StartsWith("ال", StringComparison.Ordinal)))
                {
                    forms.Add(nisbah);
                    forms.Add($"{first} {nisbah}");
                }
            }
        }

        foreach (var known in (knownAs ?? "").Split(['،', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var form = Clean(known);
            if (form.Length > 1) forms.Add(form);
        }

        return forms
            .Select(f => (Display: f, Tokens: Tokenize(f)))
            .Where(c => c.Tokens.Length > 0)
            .GroupBy(c => string.Join(' ', c.Tokens))
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>The name without annotations such as "(…)", "، وقيل …" or "ويقال …".</summary>
    private static string Clean(string? raw)
    {
        var name = (raw ?? "").Trim();
        name = Annotation().Split(name)[0].Trim();
        return name;
    }

    /// <summary>A father's or grandfather's name: one word, or two for "عبد الله", "أبي شيبة".</summary>
    private static string FirstName(string segment)
    {
        var words = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return segment;
        return words.Length > 1 && CompoundFirstWords.Contains(Tokenize(words[0]).FirstOrDefault() ?? "")
            ? $"{words[0]} {words[1]}"
            : words[0];
    }

    /// <summary>
    /// How many times the tokens occur in the text. A lone first name followed by "بن" ("محمد بن …") is the start of a
    /// longer name, not a mention of the bare name, so it is left to the longer form.
    /// </summary>
    private static int CountOccurrences(string[] text, string[] tokens)
    {
        var count = 0;
        for (var i = 0; i + tokens.Length <= text.Length; i++)
        {
            var match = true;
            for (var j = 0; j < tokens.Length && match; j++) match = text[i + j] == tokens[j];
            if (!match) continue;

            var next = i + tokens.Length;
            if (tokens.Length == 1 && next < text.Length && Connectors.Contains(text[next])) continue;
            count++;
        }
        return count;
    }

    /// <summary>Words of the text without diacritics, with alef/yaa/ta marbuta unified and "أبي/أبا" read as "أبو".</summary>
    private static string[] Tokenize(string text)
    {
        var plain = ArabicNormalizer.Normalize(text)
            .Replace("ٰ", "")
            .Replace("ـ", "")
            .Replace('ى', 'ي');
        plain = NotLetters().Replace(plain, " ");
        return plain
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w is "ابي" or "ابا" ? "ابو" : w)
            .ToArray();
    }

    [GeneratedRegex(@"(\s+(?:بن|ابن|بنت)\s+)")]
    private static partial Regex PatronymicSplit();

    [GeneratedRegex(@"\s*[\(\[\{،,]|\s+:\s+|\s+(?:ويقال|وقيل)\b")]
    private static partial Regex Annotation();

    [GeneratedRegex(@"[^\p{L}]+")]
    private static partial Regex NotLetters();
}
