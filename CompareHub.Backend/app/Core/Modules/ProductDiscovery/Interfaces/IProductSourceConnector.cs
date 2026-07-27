using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;

public interface IProductSourceConnector
{
    Task<List<ProductResultDto>> SearchAsync(
        UserProductSource source,
        string query,
        CancellationToken cancellationToken);
}
