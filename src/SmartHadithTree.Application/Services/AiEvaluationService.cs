using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.DTOs;
using Microsoft.SemanticKernel;
using System.Text.Json;

namespace SmartHadithTree.Application.Services;

public interface IAiEvaluationService
{
    Task<ExtractedAiEvaluationDto> GenerateNarratorEvaluationSummaryAsync(NarratorDetailDto narrator, CancellationToken ct = default);
}

public class AiEvaluationService(Kernel kernel) : IAiEvaluationService
{
    public async Task<ExtractedAiEvaluationDto> GenerateNarratorEvaluationSummaryAsync(NarratorDetailDto narrator, CancellationToken ct = default)
    {
        if (narrator.Evaluations == null || narrator.Evaluations.Count == 0)
        {
            return new ExtractedAiEvaluationDto
            {
                VerbatimQuote = "لا توجد أقوال مسجلة.",
                Tier = "T7", // Default to weak if unknown
                Justification = "No evaluations found."
            };
        }

        var prompt = $@"
أنت باحث محقق في علم الجرح والتعديل.
مهمتك هي قراءة أقوال العلماء التالية واستخراج الاقتباس الأكثر دقة وحسماً (يفضل أقوال ابن حجر في تقريب التهذيب أو الذهبي).
يجب عليك عدم التأليف أو التلخيص، بل استخراج النص الحرفي.
ثم، قم بتعيين الطبقة (Tier) من T1 إلى T12 بناءً على هذا القول.

T1 = صحابي
T2 = ثقة متقن
T3 = ثقة
T4 = صدوق
T5 = صدوق يهم
T6 = مقبول
T7 = ضعيف / مجهول
T8 = ضعيف جدا
T9-T11 = متروك / متهم
T12 = كذاب / وضاع

معلومات الراوي:
الاسم: {narrator.KnownAs ?? narrator.FullName}
الطبقة: {narrator.GenerationTier ?? "غير محددة"}
بلدان الإقامة والرحلة: {narrator.ResidencePlaces ?? "غير محددة"}
بلد الوفاة: {narrator.DeathPlace ?? "غير محدد"}
الرتبة في جوامع الكلم: {narrator.GawamiRank ?? "غير محددة"}
حجم المرويات: {(narrator.UniqueHadithCount.HasValue ? $"{narrator.UniqueHadithCount} حديث/طرف ({narrator.TotalNarrationsCount ?? narrator.UniqueHadithCount} إسناد)" : "غير محدد")}
ملاحظات العلل: {(narrator.IsMudallis ? "موصوف بالتدليس. " : "")}{(narrator.HasMukhtalit ? "موصوف بالاختلاط. " : "")}

أقوال العلماء:
{string.Join("\n", narrator.Evaluations.Select(e => $"- {e.ScholarName} (في كتاب {e.SourceBook ?? "غير محدد"}): {e.EvaluationText}"))}

استخرج البيانات وقم بإرجاعها ككائن JSON صالح فقط بالشكل التالي بدون أي نصوص إضافية أو علامات Markdown:
{{
  ""VerbatimQuote"": ""النص الحرفي للقول"",
  ""SourceBook"": ""اسم الكتاب أو العالم"",
  ""Tier"": ""مثال: T4"",
  ""Justification"": ""سبب اختيار هذه الطبقة باختصار شديد""
}}";

        try
        {
            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            var json = result.GetValue<string>()?.Trim();
            
            if (json != null)
            {
                // Remove markdown block if present
                if (json.StartsWith("```json")) json = json.Substring(7);
                if (json.StartsWith("```")) json = json.Substring(3);
                if (json.EndsWith("```")) json = json.Substring(0, json.Length - 3);
                json = json.Trim();

                var dto = JsonSerializer.Deserialize<ExtractedAiEvaluationDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (dto != null) return dto;
            }
            
            return new ExtractedAiEvaluationDto { VerbatimQuote = "تعذر استخراج البيانات." };
        }
        catch (Exception ex)
        {
            return new ExtractedAiEvaluationDto { VerbatimQuote = $"حدث خطأ: {ex.Message}" };
        }
    }
}
