using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Domain.Specifications;

namespace CompareHub.Backend.app.Core.Modules.SourceLinks.Specifications;

public sealed class SourceLinkByIdAndUserSpecification : BaseSpecification<UserSourceLink>
{
    public SourceLinkByIdAndUserSpecification(Guid id, Guid userId)
    {
        AddCriteria(x => x.Id == id && x.UserId == userId);
    }
}
