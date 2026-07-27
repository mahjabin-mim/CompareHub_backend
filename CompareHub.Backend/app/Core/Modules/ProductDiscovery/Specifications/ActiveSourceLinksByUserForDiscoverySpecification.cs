using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Domain.Specifications;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Specifications;

public sealed class ActiveSourceLinksByUserForDiscoverySpecification : BaseSpecification<UserSourceLink>
{
    public ActiveSourceLinksByUserForDiscoverySpecification(Guid userId)
    {
        AddCriteria(x => x.UserId == userId && x.IsActive);
    }
}
