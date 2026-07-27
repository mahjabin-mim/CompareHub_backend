using CompareHub.Backend.app.Core.Modules.ProductSources.DTOs;

namespace CompareHub.Backend.app.Core.Modules.ProductSources.Interfaces;

public interface IProductSourceService
{
    Task<List<ProductSourceDto>> GetCurrentUserSourcesAsync(CancellationToken cancellationToken);
    Task<ProductSourceDto> CreateAsync(UpsertProductSourceRequestDto request, CancellationToken cancellationToken);
    Task<ProductSourceDto> UpdateAsync(Guid id, UpsertProductSourceRequestDto request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<TestProductSourceConnectionResponseDto> TestConnectionAsync(TestProductSourceConnectionRequestDto request, CancellationToken cancellationToken);
}
