using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.DTOs;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SmartHadithTree.Application.Services;

public interface IAiEvaluationService
{
    Task<string> GenerateNarratorEvaluationSummaryAsync(NarratorDetailDto narrator, CancellationToken ct = default);
}

public class AiEvaluationService(IConfiguration config, HttpClient httpClient) : IAiEvaluationService
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

اكتب الخلاصة مباشرة دون مقدمات:
";

        var ollamaModel = config["Ollama:Model"];
        var ollamaUrl = config["Ollama:Url"] ?? "http://localhost:11434";

        if (!string.IsNullOrEmpty(ollamaModel))
        {
            return await GenerateWithOllamaAsync(prompt, ollamaModel, ollamaUrl, ct);
        }

        var apiKey = config["Gemini:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return "تعذر توليد الخلاصة. (مفتاح Gemini مفقود، ولم يتم إعداد Ollama)";
        }

        return await GenerateWithGeminiAsync(prompt, apiKey, ct);
    }

    private async Task<string> GenerateWithOllamaAsync(string prompt, string model, string baseUrl, CancellationToken ct)
    {
        var payload = new
        {
            model = model,
            prompt = prompt,
            stream = false
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/api/generate", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Ollama API Error ({response.StatusCode}): {err}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        
        try 
        {
            var text = doc.RootElement.GetProperty("response").GetString();
            return text ?? "تعذر توليد الخلاصة.";
        }
        catch
        {
            return "تعذر قراءة الاستجابة من Ollama.";
        }
    }

    private async Task<string> GenerateWithGeminiAsync(string prompt, string apiKey, CancellationToken ct)
    {
        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={apiKey}";
        
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Gemini API Error ({response.StatusCode}): {err}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        
        try 
        {
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
                
            return text ?? "تعذر توليد الخلاصة.";
        }
        catch
        {
            return "تعذر قراءة الاستجابة من Gemini.";
        }
    }
}
