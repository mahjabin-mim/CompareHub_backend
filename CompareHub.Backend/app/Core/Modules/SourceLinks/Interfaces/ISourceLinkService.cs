using CompareHub.Backend.app.Core.Modules.SourceLinks.DTOs;

namespace CompareHub.Backend.app.Core.Modules.SourceLinks.Interfaces;

public interface ISourceLinkService
{
    Task<List<SourceLinkDto>> GetCurrentUserLinksAsync(CancellationToken cancellationToken);
    Task<SourceLinkDto> CreateAsync(CreateSourceLinkRequestDto request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
