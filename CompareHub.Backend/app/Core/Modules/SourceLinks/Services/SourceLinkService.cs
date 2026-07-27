using FluentValidation;
using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Infrastructure.Auth;
using CompareHub.Backend.app.Core.Modules.SourceLinks.DTOs;
using CompareHub.Backend.app.Core.Modules.SourceLinks.Interfaces;
using CompareHub.Backend.app.Core.Modules.SourceLinks.Specifications;
using CompareHub.Backend.app.Core.Shared.Contracts;

namespace CompareHub.Backend.app.Core.Modules.SourceLinks.Services;

public class SourceLinkService : ISourceLinkService
{
    private readonly IRepository<UserSourceLink> _links;
    private readonly ICurrentUserService _currentUser;

    public SourceLinkService(IRepository<UserSourceLink> links, ICurrentUserService currentUser)
    {
        _links = links;
        _currentUser = currentUser;
    }

    public async Task<List<SourceLinkDto>> GetCurrentUserLinksAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        var entities = await _links.ListAsync(new ActiveSourceLinksByUserSpecification(userId), cancellationToken);

        return entities
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SourceLinkDto(x.Id, x.Url, x.WebsiteName, x.IsActive, x.CreatedAt))
            .ToList();
    }

    public async Task<SourceLinkDto> CreateAsync(CreateSourceLinkRequestDto request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
        {
            throw new ValidationException("Invalid source URL format.");
        }

        var entity = new UserSourceLink
        {
            UserId = userId,
            Url = request.Url,
            WebsiteName = request.WebsiteName.Trim()
        };

        await _links.AddAsync(entity, cancellationToken);
        await _links.SaveChangesAsync(cancellationToken);

        return new SourceLinkDto(entity.Id, entity.Url, entity.WebsiteName, entity.IsActive, entity.CreatedAt);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        var entity = await _links.FirstOrDefaultAsync(new SourceLinkByIdAndUserSpecification(id, userId), cancellationToken)
            ?? throw new ValidationException("Source link not found.");

        await _links.DeleteAsync(entity, cancellationToken);
        await _links.SaveChangesAsync(cancellationToken);
    }
}
