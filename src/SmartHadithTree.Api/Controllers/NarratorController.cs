using Microsoft.AspNetCore.Mvc;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.Services;

namespace SmartHadithTree.Api.Controllers;

[ApiController]
[Route("api/narrators")]
public class NarratorController(INarratorService narratorService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NarratorDetailDto>> GetNarrator(Guid id, CancellationToken ct)
    {
        var narrator = await narratorService.GetNarratorDetailsAsync(id, ct);
        if (narrator == null)
            return NotFound();

        return Ok(narrator);
    }

    [HttpGet("{id:guid}/tooltip")]
    public async Task<ActionResult<NarratorSummaryDto>> GetNarratorTooltip(Guid id, CancellationToken ct)
    {
        var tooltip = await narratorService.GetNarratorTooltipAsync(id, ct);
        if (tooltip == null)
            return NotFound();

        return Ok(tooltip);
    }

    [HttpGet("{id:guid}/ai-summary")]
    public async Task<ActionResult<string>> GetNarratorAiSummary(Guid id, [FromServices] IAiEvaluationService aiService, CancellationToken ct)
    {
        var narrator = await narratorService.GetNarratorDetailsAsync(id, ct);
        if (narrator == null)
            return NotFound();

        try
        {
            var summary = await aiService.GenerateNarratorEvaluationSummaryAsync(narrator, ct);
            return Ok(new { summary });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"AI Evaluation Failed: {ex.Message}");
        }
    }
}
