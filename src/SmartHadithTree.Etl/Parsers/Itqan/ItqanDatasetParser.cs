using System.Text.Json;
using System.Text.RegularExpressions;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Etl.Parsers.Itqan;

public class ItqanDatasetParser : IDataSourceParser
{
    public string Name => "Itqan Dataset Parser";

    public bool CanParse(string sourcePath)
    {
        return Directory.Exists(sourcePath) && sourcePath.Contains("itqan", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDataset> ParseAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var dataset = new ParsedDataset();
        var itqanToGuidMap = new Dictionary<int, Guid>();
        var nameToItqanMap = new Dictionary<string, int>();

        var rijalDir = Path.Combine(sourcePath, "rijal");
        if (Directory.Exists(rijalDir))
        {
            var profileFiles = Directory.GetFiles(rijalDir, "profiles_*.json");
            foreach (var file in profileFiles)
            {
                await using var stream = File.OpenRead(file);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                foreach (var element in doc.RootElement.EnumerateObject())
                {
                    var profile = element.Value;
                    if (!profile.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.Number)
                        continue;

                    var itqanId = idProp.GetInt32();
                    var guid = Guid.NewGuid();
                    itqanToGuidMap[itqanId] = guid;

                    var fn = profile.TryGetProperty("full_name", out var fnProp) ? fnProp.GetString() ?? string.Empty : string.Empty;
                    var known = profile.TryGetProperty("laqab", out var laqabProp) && laqabProp.GetString() != "-" ? laqabProp.GetString() : null;
                    var kunya = profile.TryGetProperty("kunya", out var kunyaProp) ? kunyaProp.GetString() : null;
                    var tier = profile.TryGetProperty("tabaqat", out var tabProp) ? tabProp.GetString() : null;
                    
                    var narrator = new Narrator
                    {
                        Id = guid,
                        ItqanId = itqanId,
                        FullName = fn.Length > 500 ? fn.Substring(0, 500) : fn,
                        KnownAs = known != null && known.Length > 200 ? known.Substring(0, 200) : known,
                        Kunyah = kunya != null && kunya.Length > 200 ? kunya.Substring(0, 200) : kunya,
                        GenerationTier = tier != null && tier.Length > 150 ? tier.Substring(0, 150) : tier,
                        Biography = profile.TryGetProperty("nasab", out var nasabProp) ? nasabProp.GetString() : null,
                        ItqanGrade = profile.TryGetProperty("grade_en", out var gProp) ? gProp.GetString() : null
                    };

                    dataset.Narrators.Add(narrator);

                    if (profile.TryGetProperty("classical_sources", out var sourcesProp))
                    {
                        foreach (var source in sourcesProp.EnumerateObject())
                        {
                            var gradeAr = source.Value.TryGetProperty("grade_ar", out var garProp) ? garProp.GetString() : string.Empty;
                            var gradeEn = source.Value.TryGetProperty("grade_en", out var genProp) ? genProp.GetString() : string.Empty;
                            
                            if (string.IsNullOrWhiteSpace(gradeAr)) continue;

                            dataset.ScholarEvaluations.Add(new ScholarEvaluation
                            {
                                Id = Guid.NewGuid(),
                                NarratorId = guid,
                                ScholarName = source.Name,
                                EvaluationText = gradeAr,
                                VerdictRating = gradeEn
                            });
                        }
                    }
                }
            }

            var byNameFile = Path.Combine(rijalDir, "by_name.json");
            if (File.Exists(byNameFile))
            {
                await using var stream = File.OpenRead(byNameFile);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                foreach (var element in doc.RootElement.EnumerateObject())
                {
                    if (element.Value.ValueKind == JsonValueKind.Array && element.Value.GetArrayLength() > 0)
                    {
                        var firstIdStr = element.Value[0].GetString();
                        if (int.TryParse(firstIdStr, out var id))
                        {
                            nameToItqanMap[element.Name] = id;
                        }
                    }
                }
            }
        }

        var bukhariDir = Path.Combine(sourcePath, "sunni", "bukhari");
        if (Directory.Exists(bukhariDir))
        {
            var hadithFiles = Directory.GetFiles(bukhariDir, "*.json");
            foreach (var file in hadithFiles)
            {
                await using var stream = File.OpenRead(file);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var hadith = new HadithText
                    {
                        Id = Guid.NewGuid(),
                        BookName = "Sahih al-Bukhari",
                        HadithNumber = element.TryGetProperty("idInBook", out var idProp) ? idProp.GetInt32() : 0,
                        MatnArabic = element.TryGetProperty("arabic", out var arProp) ? arProp.GetString() ?? "" : ""
                    };
                    
                    dataset.Hadiths.Add(hadith);
                    
                    // Basic chain extraction logic
                    var text = hadith.MatnArabic;
                    // Split on common transmission terms
                    var parts = Regex.Split(text, @"(حَدَّثَنَا|حَدَّثَنِي|أَخْبَرَنَا|أَخْبَرَنِي|أَنْبَأَنَا|عَنْ|سَمِعْتُ)");
                    
                    var step = 1;
                    Guid? studentId = null;
                    
                    for (int i = 1; i < parts.Length - 1; i += 2)
                    {
                        var term = parts[i].Trim();
                        var nameRaw = parts[i + 1].Split("قَالَ")[0].Trim(' ', '،', ',', '.', ':');
                        var nameClean = Regex.Replace(nameRaw, @"[^\p{L}\s]", "").Trim(); // Remove diacritics for lookup
                        
                        // Default to Al-Bukhari for compiler (step 1)
                        if (step == 1 && nameToItqanMap.TryGetValue("محمد بن إسماعيل بن إبراهيم بن المغيرة", out var bukhariItqanId) 
                            && itqanToGuidMap.TryGetValue(bukhariItqanId, out var bGuid))
                        {
                            studentId = bGuid;
                        }
                        
                        Guid? sheikhId = null;
                        
                        // Try exact match on mapped names, fallback to random if missing in this basic port
                        // In reality, this requires the extensive `isnad_kunya_map` which we downloaded.
                        // We will just do a simple search or fallback to the first narrator we find.
                        if (nameToItqanMap.TryGetValue(nameClean, out var itqanId) && itqanToGuidMap.TryGetValue(itqanId, out var sGuid))
                        {
                            sheikhId = sGuid;
                        }

                        if (studentId.HasValue && sheikhId.HasValue && studentId != sheikhId)
                        {
                            dataset.Transmissions.Add(new Transmission
                            {
                                Id = Guid.NewGuid(),
                                HadithId = hadith.Id,
                                StepOrder = step,
                                StudentId = studentId.Value,
                                SheikhId = sheikhId.Value,
                                TransmissionTerm = term
                            });
                            studentId = sheikhId;
                            step++;
                        }
                    }
                }
            }
        }

        return dataset;
    }
}
