using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Infrastructure.Data;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Infrastructure.Data.Repositories;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// ── Database ───────────────────────────────────────────────────────
builder.Services.AddDbContext<HadithTreeDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.CommandTimeout(60);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
        }));

// Register Semantic Kernel
var geminiApiKey = builder.Configuration["Gemini:ApiKey"];
if (!string.IsNullOrEmpty(geminiApiKey))
{
    // Use gemini-1.5-flash or gemini-2.5-flash as the model ID
    builder.Services.AddKernel()
        .AddGoogleAIGeminiChatCompletion("gemini-1.5-flash", geminiApiKey);
}

// Register Services
builder.Services.AddScoped<IHadithTreeDbContext>(provider => provider.GetRequiredService<HadithTreeDbContext>());
builder.Services.AddScoped<IHadithChainRepository, HadithChainRepository>();
builder.Services.AddScoped<IHadithSearchService, HadithSearchService>();
builder.Services.AddScoped<INarratorService, NarratorService>();
builder.Services.AddScoped<IAiEvaluationService, AiEvaluationService>();

// ── Controllers ────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Prevent Unicode-escaping Arabic characters in JSON responses.
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ── CORS (allow Next.js dev server) ────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ── Swagger (dev only) ─────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Smart Hadith Tree API — شجرة الأسانيد الذكية",
        Version = "v1",
        Description = "RESTful API for Hadith Isnad tree visualization and AI-powered narrator evaluation."
    });
});

var app = builder.Build();

// ── Middleware Pipeline ────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.MapControllers();

app.Run();
