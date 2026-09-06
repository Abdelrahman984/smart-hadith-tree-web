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

        await ParseBookDirectoryAsync(Path.Combine(sourcePath, "sunni", "bukhari"), "صحيح البخاري", GetBukhariChapterName, dataset, itqanToGuidMap, nameToItqanMap, cancellationToken);
        await ParseBookDirectoryAsync(Path.Combine(sourcePath, "sunni", "muslim"), "صحيح مسلم", GetMuslimChapterName, dataset, itqanToGuidMap, nameToItqanMap, cancellationToken);
        await ParseBookDirectoryAsync(Path.Combine(sourcePath, "sunni", "abudawud"), "سنن أبي داود", GetAbuDawudChapterName, dataset, itqanToGuidMap, nameToItqanMap, cancellationToken);
        await ParseBookDirectoryAsync(Path.Combine(sourcePath, "sunni", "tirmidhi"), "جامع الترمذي", GetTirmidhiChapterName, dataset, itqanToGuidMap, nameToItqanMap, cancellationToken);

        return dataset;
    }

    private async Task ParseBookDirectoryAsync(
        string bookDir, 
        string bookName, 
        Func<string, string> getChapterName, 
        ParsedDataset dataset, 
        Dictionary<int, Guid> itqanToGuidMap, 
        Dictionary<string, int> nameToItqanMap, 
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(bookDir)) return;

        var hadithFiles = Directory.GetFiles(bookDir, "*.json");
        foreach (var file in hadithFiles)
        {
            await using var stream = File.OpenRead(file);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var chapterName = getChapterName(fileName);

                var hadith = new HadithText
                {
                    Id = Guid.NewGuid(),
                    BookName = bookName,
                    HadithNumber = element.TryGetProperty("idInBook", out var idProp) ? idProp.GetInt32() : 0,
                    Chapter = chapterName,
                    MatnArabic = element.TryGetProperty("arabic", out var arProp) ? arProp.GetString() ?? "" : ""
                };
                
                dataset.Hadiths.Add(hadith);
                
                // Basic chain extraction logic
                var text = hadith.MatnArabic;
                var parts = Regex.Split(text, @"(حَدَّثَنَا|حَدَّثَنِي|أَخْبَرَنَا|أَخْبَرَنِي|أَنْبَأَنَا|عَنْ|سَمِعْتُ)");
                
                var step = 1;
                Guid? studentId = null;
                
                for (int i = 1; i < parts.Length - 1; i += 2)
                {
                    var term = parts[i].Trim();
                    var nameRaw = parts[i + 1].Split("قَالَ")[0].Trim(' ', '،', ',', '.', ':');
                    var nameClean = Regex.Replace(nameRaw, @"[^\p{L}\s]", "").Trim();
                    
                    if (step == 1)
                    {
                        var compilerName = bookName switch
                        {
                            "صحيح البخاري" => "محمد بن إسماعيل بن إبراهيم بن المغيرة",
                            "صحيح مسلم" => "مسلم بن الحجاج بن مسلم",
                            "سنن أبي داود" => "سليمان بن الأشعث بن إسحاق بن بشير بن شداد",
                            "جامع الترمذي" => "محمد بن عيسى بن سورة بن موسى بن الضحاك",
                            _ => ""
                        };

                        if (nameToItqanMap.TryGetValue(compilerName, out var compilerItqanId) 
                            && itqanToGuidMap.TryGetValue(compilerItqanId, out var cGuid))
                        {
                            studentId = cGuid;
                        }
                    }
                    
                    Guid? sheikhId = null;
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

    private string GetMuslimChapterName(string fileNumber)
    {
        return fileNumber switch
        {
            "0" => "المقدمة",
            "1" => "كتاب الإيمان",
            "2" => "كتاب الطهارة",
            "3" => "كتاب الحيض",
            "4" => "كتاب الصلاة",
            "5" => "كتاب المساجد ومواضع الصلاة",
            "6" => "كتاب صلاة المسافرين وقصرها",
            "7" => "كتاب الفضائل",
            "8" => "كتاب الجمعة",
            "9" => "كتاب صلاة العيدين",
            "10" => "كتاب صلاة الاستسقاء",
            "11" => "كتاب الكسوف",
            "12" => "كتاب الجنائز",
            "13" => "كتاب الزكاة",
            "14" => "كتاب الصيام",
            "15" => "كتاب الحج",
            "16" => "كتاب النكاح",
            "17" => "كتاب الرضاع",
            "18" => "كتاب الطلاق",
            "19" => "كتاب اللعان",
            "20" => "كتاب العتق",
            "21" => "كتاب البيوع",
            "22" => "كتاب المساقاة",
            "23" => "كتاب الفرائض",
            "24" => "كتاب الهبات",
            "25" => "كتاب الوصية",
            "26" => "كتاب النذور",
            "27" => "كتاب الأيمان",
            "28" => "كتاب القسامة والمحاربين والقصاص والديات",
            "29" => "كتاب الحدود",
            "30" => "كتاب الأقضية",
            "31" => "كتاب اللقطة",
            "32" => "كتاب الجهاد والسير",
            "33" => "كتاب الإمارة",
            "34" => "كتاب الصيد والذبائح وما يؤكل من الحيوان",
            "35" => "كتاب الأضاحي",
            "36" => "كتاب الأشربة",
            "37" => "كتاب اللباس والزينة",
            "38" => "كتاب الآداب",
            "39" => "كتاب السلام",
            "40" => "كتاب الألفاظ من الأدب وغيرها",
            "41" => "كتاب الشعر",
            "42" => "كتاب الرؤيا",
            "43" => "كتاب الفضائل",
            "44" => "كتاب فضائل الصحابة",
            "45" => "كتاب البر والصلة والآداب",
            "46" => "كتاب القدر",
            "47" => "كتاب العلم",
            "48" => "كتاب الذكر والدعاء والتوبة والاستغفار",
            "49" => "كتاب الرقاق",
            "50" => "كتاب التوبة",
            "51" => "كتاب صفة القيامة والجنة والنار",
            "52" => "كتاب الجنة وصفة نعيمها وأهلها",
            "53" => "كتاب الفتن وأشراط الساعة",
            "54" => "كتاب الزهد والرقائق",
            "55" => "كتاب التفسير",
            "56" => "كتاب أحاديث الأنبياء",
            _ => $"كتاب {fileNumber}"
        };
    }

    private string GetAbuDawudChapterName(string fileNumber)
    {
        return fileNumber switch
        {
            "0" => "المقدمة",
            "1" => "كتاب الطهارة",
            "2" => "كتاب الصلاة",
            "3" => "كتاب الاستسقاء",
            "4" => "كتاب صلاة السفر",
            "5" => "كتاب التطوع",
            "6" => "كتاب شهر رمضان",
            "7" => "كتاب سجود القرآن",
            "8" => "كتاب الوتر",
            "9" => "كتاب الزكاة",
            "10" => "كتاب اللقطة",
            "11" => "كتاب المناسك",
            "12" => "كتاب النكاح",
            "13" => "كتاب الطلاق",
            "14" => "كتاب الصوم",
            "15" => "كتاب الجهاد",
            "16" => "كتاب الأضحية",
            "17" => "كتاب الصيد",
            "18" => "كتاب الوصايا",
            "19" => "كتاب الفرائض",
            "20" => "كتاب الخراج والإمارة والفيء",
            "21" => "كتاب الجنائز",
            "22" => "كتاب الأيمان والنذور",
            "23" => "كتاب البيوع",
            "24" => "كتاب الإجارة",
            "25" => "كتاب الأقضية",
            "26" => "كتاب العلم",
            "27" => "كتاب الأشربة",
            "28" => "كتاب الأطعمة",
            "29" => "كتاب الطب",
            "30" => "كتاب الكهانة والتطير",
            "31" => "كتاب العتق",
            "32" => "كتاب الحروف والقراءات",
            "33" => "كتاب الحمامات",
            "34" => "كتاب اللباس",
            "35" => "كتاب الترجل",
            "36" => "كتاب الخاتم",
            "37" => "كتاب الفتن والملاحم",
            "38" => "كتاب المهدي",
            "39" => "كتاب الملاحم",
            "40" => "كتاب الحدود",
            "41" => "كتاب الديات",
            "42" => "كتاب السنة",
            "43" => "كتاب الأدب",
            _ => $"كتاب {fileNumber}"
        };
    }

    private string GetTirmidhiChapterName(string fileNumber)
    {
        return fileNumber switch
        {
            "1" => "كتاب الطهارة",
            "2" => "كتاب الصلاة",
            "3" => "كتاب الوتر",
            "4" => "كتاب الجمعة",
            "5" => "كتاب الزكاة",
            "6" => "كتاب الصوم",
            "7" => "كتاب الحج",
            "8" => "كتاب الجنائز",
            "9" => "كتاب النكاح",
            "10" => "كتاب الرضاع",
            "11" => "كتاب الطلاق واللعان",
            "12" => "كتاب البيوع",
            "13" => "كتاب الأحكام",
            "14" => "كتاب الديات",
            "15" => "كتاب الحدود",
            "16" => "كتاب الصيد والذبائح",
            "17" => "كتاب الأضاحي",
            "18" => "كتاب النذور والأيمان",
            "19" => "كتاب السير",
            "20" => "كتاب فضائل الجهاد",
            "21" => "كتاب الجهاد",
            "22" => "كتاب اللباس",
            "23" => "كتاب الأطعمة",
            "24" => "كتاب الأشربة",
            "25" => "كتاب البر والصلة",
            "26" => "كتاب الطب",
            "27" => "كتاب الفرائض",
            "28" => "كتاب الوصايا",
            "29" => "كتاب الولاء والهبة",
            "30" => "كتاب القدر",
            "31" => "كتاب الفتن",
            "32" => "كتاب الرؤيا",
            "33" => "كتاب الشهادات",
            "34" => "كتاب الزهد",
            "35" => "كتاب صفة القيامة",
            "36" => "كتاب صفة الجنة",
            "37" => "كتاب صفة جهنم",
            "38" => "كتاب الإيمان",
            "39" => "كتاب العلم",
            "40" => "كتاب الاستئذان والآداب",
            "41" => "كتاب الأدب",
            "42" => "كتاب الأمثال",
            "43" => "كتاب ثواب القرآن",
            "44" => "كتاب القراءات",
            "45" => "كتاب التفسير",
            "46" => "كتاب الدعوات",
            "47" => "كتاب المناقب",
            "48" => "كتاب فضائل الصحابة",
            "49" => "كتاب العلل",
            _ => $"كتاب {fileNumber}"
        };
    }
}
