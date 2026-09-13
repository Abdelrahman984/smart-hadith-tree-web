using Microsoft.AspNetCore.Mvc;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController(IHadithSearchService searchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<HadithSearchResultDto>>> Search([FromQuery] SearchRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query) && (request.Phrases == null || request.Phrases.Count == 0))
            return BadRequest("Search query cannot be empty.");

        var results = await searchService.SearchHadithsAsync(request, ct);
        return Ok(results);
    }

    [HttpPost("advanced")]
    public async Task<ActionResult<List<HadithSearchResultDto>>> AdvancedSearch([FromBody] SearchRequestDto request, CancellationToken ct)
    {
        var results = await searchService.SearchHadithsAsync(request, ct);
        return Ok(results);
    }
}
