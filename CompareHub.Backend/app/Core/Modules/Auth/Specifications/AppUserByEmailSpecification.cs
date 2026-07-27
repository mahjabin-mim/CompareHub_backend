using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Domain.Specifications;

namespace CompareHub.Backend.app.Core.Modules.Auth.Specifications;

public sealed class AppUserByEmailSpecification : BaseSpecification<AppUser>
{
    public AppUserByEmailSpecification(string email)
    {
        AddCriteria(x => x.Email == email);
    }
}
