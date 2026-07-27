using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Domain.Specifications;

namespace CompareHub.Backend.app.Core.Modules.SourceLinks.Specifications;

public sealed class ActiveSourceLinksByUserSpecification : BaseSpecification<UserSourceLink>
{
    public ActiveSourceLinksByUserSpecification(Guid userId)
    {
        AddCriteria(x => x.UserId == userId && x.IsActive);
    }
}
