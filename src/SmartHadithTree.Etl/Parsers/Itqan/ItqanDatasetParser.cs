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
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var chapterName = GetBukhariChapterName(fileName);

                    var hadith = new HadithText
                    {
                        Id = Guid.NewGuid(),
                        BookName = "صحيح البخاري",
                        HadithNumber = element.TryGetProperty("idInBook", out var idProp) ? idProp.GetInt32() : 0,
                        Chapter = chapterName,
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

    private string GetBukhariChapterName(string fileNumber)
    {
        return fileNumber switch
        {
            "0" => "المقدمة",
            "1" => "كتاب بدء الوحي",
            "2" => "كتاب الإيمان",
            "3" => "كتاب العلم",
            "4" => "كتاب الوضوء",
            "5" => "كتاب الغسل",
            "6" => "كتاب الحيض",
            "7" => "كتاب التيمم",
            "8" => "كتاب الصلاة",
            "9" => "كتاب مواقيت الصلاة",
            "10" => "كتاب الأذان",
            "11" => "كتاب الجمعة",
            "12" => "كتاب صلاة الخوف",
            "13" => "كتاب العيدين",
            "14" => "كتاب الوتر",
            "15" => "كتاب الاستسقاء",
            "16" => "كتاب الكسوف",
            "17" => "كتاب سجود القرآن",
            "18" => "كتاب تقصير الصلاة",
            "19" => "كتاب التهجد",
            "20" => "كتاب فضل الصلاة في مسجد مكة والمدينة",
            "21" => "كتاب العمل في الصلاة",
            "22" => "كتاب السهو",
            "23" => "كتاب الجنائز",
            "24" => "كتاب الزكاة",
            "25" => "كتاب الحج",
            "26" => "كتاب العمرة",
            "27" => "كتاب المحصر",
            "28" => "كتاب جزاء الصيد",
            "29" => "كتاب فضائل المدينة",
            "30" => "كتاب الصوم",
            "31" => "كتاب صلاة التراويح",
            "32" => "كتاب فضل ليلة القدر",
            "33" => "كتاب الاعتكاف",
            "34" => "كتاب البيوع",
            "35" => "كتاب السلم",
            "36" => "كتاب الشفعة",
            "37" => "كتاب الإجارة",
            "38" => "كتاب الحوالات",
            "39" => "كتاب الكفالة",
            "40" => "كتاب الوكالة",
            "41" => "كتاب المزارعة",
            "42" => "كتاب المساقاة",
            "43" => "كتاب الاستقراض",
            "44" => "كتاب الخصومات",
            "45" => "كتاب اللقطة",
            "46" => "كتاب المظالم",
            "47" => "كتاب الشركة",
            "48" => "كتاب الرهن",
            "49" => "كتاب العتق",
            "50" => "كتاب المكاتب",
            "51" => "كتاب الهبة",
            "52" => "كتاب الشهادات",
            "53" => "كتاب الصلح",
            "54" => "كتاب الشروط",
            "55" => "كتاب الوصايا",
            "56" => "كتاب الجهاد والسير",
            "57" => "كتاب فرض الخمس",
            "58" => "كتاب الجزية والموادعة",
            "59" => "كتاب بدء الخلق",
            "60" => "كتاب أحاديث الأنبياء",
            "61" => "كتاب المناقب",
            "62" => "كتاب فضائل أصحاب النبي",
            "63" => "كتاب مناقب الأنصار",
            "64" => "كتاب المغازي",
            "65" => "كتاب التفسير",
            "66" => "كتاب فضائل القرآن",
            "67" => "كتاب النكاح",
            "68" => "كتاب الطلاق",
            "69" => "كتاب النفقات",
            "70" => "كتاب الأطعمة",
            "71" => "كتاب العقيقة",
            "72" => "كتاب الذبائح والصيد",
            "73" => "كتاب الأضاحي",
            "74" => "كتاب الأشربة",
            "75" => "كتاب المرضى",
            "76" => "كتاب الطب",
            "77" => "كتاب اللباس",
            "78" => "كتاب الأدب",
            "79" => "كتاب الاستئذان",
            "80" => "كتاب الدعوات",
            "81" => "كتاب الرقاق",
            "82" => "كتاب القدر",
            "83" => "كتاب الأيمان والنذور",
            "84" => "كتاب كفارات الأيمان",
            "85" => "كتاب الفرائض",
            "86" => "كتاب الحدود",
            "87" => "كتاب الديات",
            "88" => "كتاب استتابة المرتدين والمعاندين وقتالهم",
            "89" => "كتاب الإكراه",
            "90" => "كتاب الحيل",
            "91" => "كتاب التعبير",
            "92" => "كتاب الفتن",
            "93" => "كتاب الأحكام",
            "94" => "كتاب التمني",
            "95" => "كتاب أخبار الآحاد",
            "96" => "كتاب الاعتصام بالكتاب والسنة",
            "97" => "كتاب التوحيد",
            _ => $"كتاب {fileNumber}"
        };
    }
}
