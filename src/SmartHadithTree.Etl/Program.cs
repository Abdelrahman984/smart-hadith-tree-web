using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Etl.Parsers;
using SmartHadithTree.Etl.Services;
using SmartHadithTree.Infrastructure.Data;

// ── Build the Host ─────────────────────────────────────────────────
var builder = Host.CreateApplicationBuilder(args);

// Load configuration from appsettings.json
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);

// ── Register Services ──────────────────────────────────────────────
builder.Services.AddDbContext<HadithTreeDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.CommandTimeout(600))); // 10 minutes for bulk operations

// Parsers (pluggable — add new parsers here)
builder.Services.AddTransient<IDataSourceParser, SmartHadithTree.Etl.Parsers.Itqan.ItqanDatasetParser>();
builder.Services.AddTransient<IDataSourceParser, SmartHadithTree.Etl.Parsers.Shamela.ShamelaSqliteParser>();
builder.Services.AddTransient<IDataSourceParser, SmartHadithTree.Etl.Parsers.Shamela.ShamelaJsonParser>();
builder.Services.AddTransient<IDataSourceParser, SeedDataGenerator>();

// Services
builder.Services.AddSingleton<SmartHadithTree.Etl.Parsers.Itqan.ContextualDisambiguator>();
builder.Services.AddTransient<BulkDataIngestionService>();
builder.Services.AddTransient<ChainReprocessingService>();
builder.Services.AddTransient<IlalSeedService>();
builder.Services.AddTransient<EtlOrchestrator>();

var host = builder.Build();

// ── Run the ETL Pipeline ───────────────────────────────────────────
using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
var orchestrator = scope.ServiceProvider.GetRequiredService<EtlOrchestrator>();
var reprocessor = scope.ServiceProvider.GetRequiredService<ChainReprocessingService>();

// Determine the data source from command-line args or default to "seed"
var source = args.Length > 0 ? args[0] : "seed";

logger.LogInformation("╔══════════════════════════════════════════════╗");
logger.LogInformation("║   Smart Hadith Tree — ETL Data Pipeline     ║");
logger.LogInformation("║   شجرة الأسانيد الذكية — خط أنابيب البيانات ║");
logger.LogInformation("╚══════════════════════════════════════════════╝");
logger.LogInformation("Source: {Source}", source);

try
{
    if (source.Equals("reprocess-chains", StringComparison.OrdinalIgnoreCase) ||
        source.Equals("reprocess-missing-chains", StringComparison.OrdinalIgnoreCase))
    {
        // Require the path to itqan data. E.g. dotnet run reprocess-chains data/itqan
        var itqanPath = args.Length > 1 ? args[1] : "data/itqan";
        var bookFilter = args.Length > 2 ? args[2] : null;
        bool onlyMissing = source.Equals("reprocess-missing-chains", StringComparison.OrdinalIgnoreCase);
        await reprocessor.ReprocessChainsAsync(itqanPath, bookFilter, CancellationToken.None, onlyMissing);
    }
    else if (source.Equals("seed-ilal", StringComparison.OrdinalIgnoreCase))
    {
        // Teacher/student relations + mudallisin/mukhtalitun seeds. E.g. dotnet run seed-ilal data/itqan
        var itqanPath = args.Length > 1 ? args[1] : "data/itqan";
        var seedsPath = Path.Combine(AppContext.BaseDirectory, "Seeds");
        var ilalSeeder = scope.ServiceProvider.GetRequiredService<IlalSeedService>();
        await ilalSeeder.RunAsync(itqanPath, seedsPath, CancellationToken.None);
    }
    else
    {
        await orchestrator.RunAsync(source, CancellationToken.None);
    }
    
    logger.LogInformation("ETL completed successfully. ✓");
    return 0;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "ETL pipeline failed with a critical error.");
    return 1;
}
