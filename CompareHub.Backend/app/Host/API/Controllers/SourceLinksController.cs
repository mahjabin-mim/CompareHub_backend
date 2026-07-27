using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CompareHub.Backend.app.Core.Modules.SourceLinks.DTOs;
using CompareHub.Backend.app.Core.Modules.SourceLinks.Interfaces;
using CompareHub.Backend.app.Core.Shared.Common;

namespace CompareHub.Backend.app.Host.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/source-links")]
public class SourceLinksController : ControllerBase
{
    private readonly ISourceLinkService _sourceLinkService;

    public SourceLinksController(ISourceLinkService sourceLinkService)
    {
        _sourceLinkService = sourceLinkService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await _sourceLinkService.GetCurrentUserLinksAsync(cancellationToken);
        return Ok(ApiResponse<List<SourceLinkDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSourceLinkRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _sourceLinkService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<SourceLinkDto>.Ok(result, "Source link created."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await _sourceLinkService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<string>.Ok("Deleted", "Source link deleted."));
    }
}
