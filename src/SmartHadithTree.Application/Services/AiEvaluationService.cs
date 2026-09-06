using Microsoft.SemanticKernel;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.DTOs;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

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

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        var prompt = @"
أنت عالم جرح وتعديل متخصص في علم الحديث النبوي.
لديك قائمة بأقوال العلماء (الجرح والتعديل) في راوٍ معين.
مهمتك هي قراءة هذه الأقوال، وتلخيص حال الراوي في فقرة واحدة موجزة ودقيقة باللغة العربية.
يجب أن تعطي الحكم النهائي (مثال: ثقة، صدوق، ضعيف، متروك) بناءً على أغلبية الأقوال وقوتها، ثم تبرر ذلك باختصار شديد.

معلومات الراوي:
الاسم: {{ $name }}
الطبقة: {{ $tier }}

أقوال العلماء:
{{ $evaluations }}

اكتب الخلاصة مباشرة دون مقدمات:
";

        // Serialize evaluations to a neat string
        var evaluationsText = string.Join("\n", narrator.Evaluations.Select(e => $"- {e.ScholarName}: {e.EvaluationText} (الحكم: {e.VerdictRating ?? "غير محدد"})"));

        var arguments = new KernelArguments
        {
            ["name"] = narrator.KnownAs ?? narrator.FullName,
            ["tier"] = narrator.GenerationTier ?? "غير محددة",
            ["evaluations"] = evaluationsText
        };

        var function = kernel.CreateFunctionFromPrompt(prompt);
        var result = await kernel.InvokeAsync(function, arguments, ct);

        return result.GetValue<string>() ?? "تعذر توليد الخلاصة.";
    }
}
