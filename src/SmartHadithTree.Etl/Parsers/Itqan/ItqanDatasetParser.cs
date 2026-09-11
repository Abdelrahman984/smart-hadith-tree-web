using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Utilities;
using SmartHadithTree.Infrastructure.Data;

namespace SmartHadithTree.Etl.Parsers.Itqan;

public class ItqanDatasetParser : IDataSourceParser
{
    private readonly HadithTreeDbContext? _dbContext;
    private readonly ILogger<ItqanDatasetParser>? _logger;

    public ItqanDatasetParser() : this(null, null) { }

    public ItqanDatasetParser(HadithTreeDbContext? dbContext = null, ILogger<ItqanDatasetParser>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string Name => "Itqan Dataset Parser";

    public bool CanParse(string sourcePath)
    {
        return Directory.Exists(sourcePath) && sourcePath.Contains("itqan", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly Dictionary<string, (string ArabicName, int CompilerItqanId, string CompilerName)> BookMetadata = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bukhari"] = ("صحيح البخاري", 55562, "محمد بن إسماعيل بن إبراهيم بن المغيرة"),
        ["muslim"] = ("صحيح مسلم", 618, "مسلم بن الحجاج بن مسلم"),
        ["abudawud"] = ("سنن أبي داود", 74, "سليمان بن الأشعث"),
        ["tirmidhi"] = ("جامع الترمذي", 69584, "محمد بن عيسى بن سورة بن موسى بن الضحاك"),
        ["nasai"] = ("سنن النسائي", 57802, "أحمد بن شعيب بن علي بن سنان بن بحر"),
        ["ibnmajah"] = ("سنن ابن ماجه", 64080, "محمد بن يزيد بن ماجه"),
        ["ahmed"] = ("مسند أحمد", 12657, "أحمد بن محمد بن حنبل"),
        ["malik"] = ("موطأ مالك", 60209, "مالك بن أنس"),
        ["darimi"] = ("سنن الدارمي", 56570, "عبد الله بن عبد الرحمن بن الفضل بن بهرام"),
        ["nawawi40"] = ("الأربعون النووية", 58153, "يحيى بن شرف بن مري بن حسن النووي"),
        ["qudsi40"] = ("الأربعون القدسية", 0, ""),
        ["shahwaliullah40"] = ("أربعون شاه ولي الله", 0, ""),
        ["riyad_assalihin"] = ("رياض الصالحين", 58153, "يحيى بن شرف بن مري بن حسن النووي"),
        ["aladab_almufrad"] = ("الأدب المفرد", 55562, "محمد بن إسماعيل بن إبراهيم بن المغيرة"),
        ["bulugh_almaram"] = ("بلوغ المرام", 1642, "أحمد بن علي بن محمد بن محمد بن علي بن أحمد"),
        ["mishkat_almasabih"] = ("مشكاة المصابيح", 0, ""),
        ["shamail_muhammadiyah"] = ("الشمائل المحمدية", 69584, "محمد بن عيسى بن سورة بن موسى بن الضحاك"),
        ["musannaf_ibnabi_shaybah"] = ("مصنف ابن أبي شيبة", 57598, "عبد الله بن محمد بن إبراهيم بن عثمان")
    };

    /// <summary>
    /// Prominent ambiguous narrator keys in by_name.json that should not blindly take index [0].
    /// Resolves canonical Hadith scholars (e.g. Sufyan ibn Uyaynah / al-Thawri, Yahya al-Ansari, Muhammad ibn Kathir al-Abdi).
    /// </summary>
    private static readonly Dictionary<string, int> DisambiguationOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["سفيان"] = 192, // سفيان بن عيينة (or 434 سفيان الثوري depending on chain, but never the obscure Sahabi 3362)
        ["محمد بن كثير"] = 1191, // محمد بن كثير العبدي (شيخ أبي داود), not 778 (محمد بن بشر)
        ["يحيى بن سعيد"] = 199, // يحيى بن سعيد الأنصاري (المدار المشهور), not 87 (يحيى بن سعيد الأموي)
    };

    public async Task<ParsedDataset> ParseAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var dataset = new ParsedDataset();
        var itqanToGuidMap = new Dictionary<int, Guid>();
        var nameToItqanMap = new Dictionary<string, int>();

        // 1. Check existing narrators from database
        var existingNarratorsCount = 0;
        if (_dbContext != null)
        {
            var existingNarrators = await _dbContext.Narrators
                .Where(n => n.ItqanId != null)
                .Select(n => new { ItqanId = n.ItqanId!.Value, n.Id })
                .ToListAsync(cancellationToken);

            if (existingNarrators.Count > 0)
            {
                existingNarratorsCount = existingNarrators.Count;
                _logger?.LogInformation("Loaded {Count} existing narrator mappings from database.", existingNarrators.Count);
                foreach (var n in existingNarrators)
                {
                    itqanToGuidMap[n.ItqanId] = n.Id;
                }
            }
        }

        var rijalDir = Path.Combine(sourcePath, "rijal");
        if (Directory.Exists(rijalDir))
        {
            // Only parse profiles if not already in DB
            if (existingNarratorsCount == 0)
            {
                _logger?.LogInformation("Parsing rijal profiles from {RijalDir}...", rijalDir);
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
            }

            // Always parse by_name.json for text-to-ID mapping
            var byNameFile = Path.Combine(rijalDir, "by_name.json");
            if (File.Exists(byNameFile))
            {
                await using var stream = File.OpenRead(byNameFile);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                foreach (var element in doc.RootElement.EnumerateObject())
                {
                    if (DisambiguationOverrides.TryGetValue(element.Name, out var overrideId))
                    {
                        nameToItqanMap[element.Name] = overrideId;
                    }
                    else if (element.Value.ValueKind == JsonValueKind.Array && element.Value.GetArrayLength() > 0)
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

        // 2. Determine existing books in database to avoid duplicate ingestion
        var existingBooks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_dbContext != null)
        {
            var booksFromDb = await _dbContext.Hadiths
                .Where(h => !string.IsNullOrEmpty(h.BookName))
                .Select(h => h.BookName)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var b in booksFromDb)
            {
                existingBooks.Add(b);
                existingBooks.Add(ArabicNormalizer.Normalize(b));
            }
            _logger?.LogInformation("Found {Count} existing books in database: {Books}", booksFromDb.Count, string.Join(", ", booksFromDb));
        }

        // 3. Dynamically discover and parse books in sunni/ directory
        var sunniDir = Path.Combine(sourcePath, "sunni");
        if (Directory.Exists(sunniDir))
        {
            var bookDirs = Directory.GetDirectories(sunniDir);
            foreach (var dir in bookDirs)
            {
                var dirName = Path.GetFileName(dir);
                var arabicName = dirName;
                var compilerItqanId = 0;
                var compilerName = "";

                if (BookMetadata.TryGetValue(dirName, out var meta))
                {
                    arabicName = meta.ArabicName;
                    compilerItqanId = meta.CompilerItqanId;
                    compilerName = meta.CompilerName;
                }

                if (existingBooks.Contains(arabicName) || existingBooks.Contains(ArabicNormalizer.Normalize(arabicName)))
                {
                    _logger?.LogInformation("Book '{BookName}' already exists in database. Skipping.", arabicName);
                    continue;
                }

                _logger?.LogInformation("Parsing book: {BookName} ({Dir})...", arabicName, dirName);
                await ParseBookDirectoryAsync(dir, arabicName, compilerItqanId, compilerName, dataset, itqanToGuidMap, nameToItqanMap, cancellationToken);
            }
        }

        return dataset;
    }

    private async Task ParseBookDirectoryAsync(
        string bookDir, 
        string bookName, 
        int compilerItqanId,
        string compilerName,
        ParsedDataset dataset, 
        Dictionary<int, Guid> itqanToGuidMap, 
        Dictionary<string, int> nameToItqanMap, 
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(bookDir)) return;

        // Load index.json if present for authentic chapter names
        var chapterMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var indexFile = Path.Combine(bookDir, "index.json");
        if (File.Exists(indexFile))
        {
            try
            {
                await using var idxStream = File.OpenRead(indexFile);
                using var idxDoc = await JsonDocument.ParseAsync(idxStream, cancellationToken: cancellationToken);
                foreach (var el in idxDoc.RootElement.EnumerateArray())
                {
                    var fileProp = el.TryGetProperty("file", out var f) ? f.GetString() : null;
                    var nameArProp = el.TryGetProperty("name_ar", out var nar) ? nar.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(fileProp) && !string.IsNullOrWhiteSpace(nameArProp))
                    {
                        chapterMap[fileProp] = nameArProp;
                        chapterMap[Path.GetFileNameWithoutExtension(fileProp)] = nameArProp;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to parse index.json in {BookDir}", bookDir);
            }
        }

        var hadithFiles = Directory.GetFiles(bookDir, "*.json")
            .Where(f => int.TryParse(Path.GetFileNameWithoutExtension(f), out _))
            .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f)))
            .ToList();

        foreach (var file in hadithFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            string chapterName;
            if (chapterMap.TryGetValue(fileName, out var cn) || chapterMap.TryGetValue(Path.GetFileName(file), out cn))
            {
                chapterName = cn;
            }
            else
            {
                chapterName = GetFallbackChapterName(bookName, fileName);
            }

            await using var stream = File.OpenRead(file);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                continue;
            
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                int hadithNumber = 0;
                if (element.TryGetProperty("idInBook", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                    hadithNumber = idProp.GetInt32();
                else if (element.TryGetProperty("hadithNumber", out var hnProp) && hnProp.ValueKind == JsonValueKind.Number)
                    hadithNumber = hnProp.GetInt32();
                else if (element.TryGetProperty("id", out var id2Prop) && id2Prop.ValueKind == JsonValueKind.Number)
                    hadithNumber = id2Prop.GetInt32();

                var matn = element.TryGetProperty("arabic", out var arProp) ? arProp.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(matn)) continue;

                var hadith = new HadithText
                {
                    Id = Guid.NewGuid(),
                    BookName = bookName,
                    NormalizedBookName = ArabicNormalizer.Normalize(bookName),
                    HadithNumber = hadithNumber,
                    Chapter = chapterName,
                    MatnArabic = matn,
                    NormalizedMatn = ArabicNormalizer.Normalize(matn)
                };
                
                dataset.Hadiths.Add(hadith);
                
                // Chain extraction logic: capture common transmission phrases and respect word boundaries
                var parts = Regex.Split(matn, @"(حَدَّثَنَا|حَدَّثَنِي|أَخْبَرَنَا|أَخْبَرَنِي|أَنْبَأَنَا|أَنَّهُ\s+سَمِعَ|أَنَّهَا\s+سَمِعَتْ|سَمِعْتُ|سَمِعَ|سَمِعَتْ|\bعَنْ\b)");
                var step = 1;
                Guid? studentId = null;

                if (compilerItqanId != 0 && itqanToGuidMap.TryGetValue(compilerItqanId, out var cGuid))
                {
                    studentId = cGuid;
                }
                else if (!string.IsNullOrEmpty(compilerName) && nameToItqanMap.TryGetValue(compilerName, out var cId) && itqanToGuidMap.TryGetValue(cId, out var cGuid2))
                {
                    studentId = cGuid2;
                }

                for (int i = 1; i < parts.Length - 1; i += 2)
                {
                    var term = parts[i].Trim();
                    var nameRaw = parts[i + 1];

                    // Remove honorific and prayer expressions
                    nameRaw = Regex.Replace(nameRaw, @"(رَضِيَ\s+اللَّهُ\s+عَنْهُ|رَضِيَ\s+اللَّهُ\s+عَنْهُمَا|رَضِيَ\s+اللَّهُ\s+عَنْهَا|رَضِيَ\s+اللَّهُ\s+عَنْهُمْ|صَلَّى\s+اللَّهُ\s+عَلَيْهِ\s+وَسَلَّمَ|عَلَيْهِ\s+السَّلَامُ|رَحِمَهُ\s+اللَّهُ)", "");

                    // Split on narrative and speech boundaries
                    nameRaw = Regex.Split(nameRaw, @"(قَالَ|يَقُولُ|أَنَّهُ|أَنَّ|أَنَّهَا|عَلَى\s+الْمِنْبَرِ|وَهُوَ\s+عَلَى\s+الْمِنْبَرِ)")[0];

                    nameRaw = nameRaw.Trim(' ', '،', ',', '.', ':', '؛');
                    var nameClean = Regex.Replace(nameRaw, @"[^\p{L}\s]", "").Trim();
                    nameClean = Regex.Replace(nameClean, @"\s+", " ");

                    Guid? sheikhId = null;
                    if (nameToItqanMap.TryGetValue(nameClean, out var itqanId) && itqanToGuidMap.TryGetValue(itqanId, out var sGuid))
                    {
                        sheikhId = sGuid;
                    }

                    if (sheikhId.HasValue)
                    {
                        if (studentId.HasValue && studentId != sheikhId)
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
                            step++;
                        }
                        studentId = sheikhId;
                    }
                }
            }
        }
    }

    private string GetFallbackChapterName(string bookName, string fileNumber)
    {
        return bookName switch
        {
            "صحيح البخاري" => GetBukhariChapterName(fileNumber),
            "صحيح مسلم" => GetMuslimChapterName(fileNumber),
            "سنن أبي داود" => GetAbuDawudChapterName(fileNumber),
            "جامع الترمذي" => GetTirmidhiChapterName(fileNumber),
            _ => $"كتاب {fileNumber}"
        };
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
