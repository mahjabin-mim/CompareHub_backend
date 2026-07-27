using System.Linq.Expressions;

namespace CompareHub.Backend.app.Core.Domain.Specifications;

public interface ISpecification<T>
{
    Expression<Func<T, bool>>? Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
}
