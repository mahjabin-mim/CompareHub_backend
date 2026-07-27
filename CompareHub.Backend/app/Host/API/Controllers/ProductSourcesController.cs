using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CompareHub.Backend.app.Core.Modules.ProductSources.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductSources.Interfaces;
using CompareHub.Backend.app.Core.Shared.Common;

namespace CompareHub.Backend.app.Host.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/product-sources")]
public class ProductSourcesController : ControllerBase
{
    private readonly IProductSourceService _productSourceService;

    public ProductSourcesController(IProductSourceService productSourceService)
    {
        _productSourceService = productSourceService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await _productSourceService.GetCurrentUserSourcesAsync(cancellationToken);
        return Ok(ApiResponse<List<ProductSourceDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertProductSourceRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _productSourceService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<ProductSourceDto>.Ok(result, "Product source created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpsertProductSourceRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _productSourceService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<ProductSourceDto>.Ok(result, "Product source updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await _productSourceService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<string>.Ok("Deleted", "Product source deleted."));
    }

    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection([FromBody] TestProductSourceConnectionRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _productSourceService.TestConnectionAsync(request, cancellationToken);
        return Ok(ApiResponse<TestProductSourceConnectionResponseDto>.Ok(result));
    }
}
