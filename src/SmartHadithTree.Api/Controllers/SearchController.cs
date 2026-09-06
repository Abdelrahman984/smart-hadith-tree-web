using Microsoft.AspNetCore.Mvc;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController(IHadithSearchService searchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<HadithSearchResultDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Search query cannot be empty.");

        var results = await searchService.SearchHadithsAsync(q, ct);
        return Ok(results);
    }
}
