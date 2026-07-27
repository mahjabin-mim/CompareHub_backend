using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Domain.Specifications;

namespace CompareHub.Backend.app.Core.Modules.ProductSources.Specifications;

public sealed class ProductSourceByIdAndUserSpecification : BaseSpecification<UserProductSource>
{
    public ProductSourceByIdAndUserSpecification(Guid id, Guid userId)
    {
        AddCriteria(x => x.Id == id && x.UserId == userId);
    }
}
