using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Domain.Specifications;

namespace CompareHub.Backend.app.Core.Modules.ProductSources.Specifications;

public sealed class ActiveProductSourcesByUserSpecification : BaseSpecification<UserProductSource>
{
    public ActiveProductSourcesByUserSpecification(Guid userId)
    {
        AddCriteria(x => x.UserId == userId && x.IsActive);
    }
}
