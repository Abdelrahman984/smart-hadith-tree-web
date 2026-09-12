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
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Search query cannot be empty.");

        var results = await searchService.SearchHadithsAsync(request, ct);
        return Ok(results);
    }
}
