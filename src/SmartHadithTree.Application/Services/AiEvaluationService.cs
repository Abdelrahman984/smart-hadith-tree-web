using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.DTOs;
using Microsoft.SemanticKernel;

namespace SmartHadithTree.Application.Services;

public interface IAiEvaluationService
{
    Task<string> GenerateNarratorEvaluationSummaryAsync(NarratorDetailDto narrator, CancellationToken ct = default);
}

public class AiEvaluationService(Kernel kernel) : IAiEvaluationService
{
    public async Task<string> GenerateNarratorEvaluationSummaryAsync(NarratorDetailDto narrator, CancellationToken ct = default)
    {
        if (narrator.Evaluations == null || narrator.Evaluations.Count == 0)
        {
            return "لا توجد أقوال مسجلة لهذا الراوي لاستنتاج حكم عام.";
        }

        var prompt = $@"
أنت عالم جرح وتعديل متخصص في علم الحديث النبوي.
لديك قائمة بأقوال العلماء (الجرح والتعديل) في راوٍ معين.
مهمتك هي قراءة هذه الأقوال، وتلخيص حال الراوي في فقرة واحدة موجزة ودقيقة باللغة العربية.
يجب أن تعطي الحكم النهائي (مثال: ثقة، صدوق، ضعيف، متروك) بناءً على أغلبية الأقوال وقوتها، ثم تبرر ذلك باختصار شديد.

معلومات الراوي:
الاسم: {narrator.KnownAs ?? narrator.FullName}
الطبقة: {narrator.GenerationTier ?? "غير محددة"}

أقوال العلماء:
{string.Join("\n", narrator.Evaluations.Select(e => $"- {e.ScholarName}: {e.EvaluationText} (الحكم: {e.VerdictRating ?? "غير محدد"})"))}

اكتب الخلاصة مباشرة دون مقدمات:";

        try
        {
            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            return result.GetValue<string>() ?? "تعذر توليد الخلاصة.";
        }
        catch (Exception ex)
        {
            return $"حدث خطأ أثناء تقييم الذكاء الاصطناعي: {ex.Message}";
        }
    }
}
