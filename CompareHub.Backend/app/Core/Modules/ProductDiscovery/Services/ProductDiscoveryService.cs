using FluentValidation;
using System.Text.RegularExpressions;
using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Infrastructure.Auth;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductSources.Specifications;
using CompareHub.Backend.app.Core.Shared.Contracts;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Services;

public class ProductDiscoveryService : IProductDiscoveryService
{
    private readonly IRepository<UserProductSource> _sources;
    private readonly ICurrentUserService _currentUser;
    private readonly IProductSourceConnector _productSourceConnector;
    private readonly ILogger<ProductDiscoveryService> _logger;

    public ProductDiscoveryService(
        IRepository<UserProductSource> sources,
        ICurrentUserService currentUser,
        IProductSourceConnector productSourceConnector,
        ILogger<ProductDiscoveryService> logger)
    {
        _sources = sources;
        _currentUser = currentUser;
        _productSourceConnector = productSourceConnector;
        _logger = logger;
    }

    public async Task<ProductSearchResponseDto> SearchProductsAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ValidationException("Search query is required.");
        }

        var userId = _currentUser.GetUserId();
        var sources = await _sources.ListAsync(new ActiveProductSourcesByUserSpecification(userId), cancellationToken);
        if (sources.Count == 0)
        {
            throw new ValidationException("No active product sources found. Add product sources first.");
        }

        var failedSources = new List<string>();
        var searchTasks = sources.Select(async source =>
        {
            try
            {
                return await _productSourceConnector.SearchAsync(source, query.Trim(), cancellationToken);
            }
            catch (Exception ex)
            {
                // One failing source should not fail the entire aggregation.
                _logger.LogWarning(ex, "Source {SourceName} failed during product search.", source.SourceName);
                lock (failedSources)
                {
                    failedSources.Add(source.SourceName);
                }
                return [];
            }
        });
        var sourceResults = await Task.WhenAll(searchTasks);
        var queryTokens = BuildQueryTokens(query);

        var scored = sourceResults
            .SelectMany(x => x)
            .Select(x => new
            {
                Product = x,
                Relevance = CalculateRelevanceScore(x.ProductName, queryTokens)
            })
            .ToList();

        // Keep strict relevance when we have clear matches, but do not hide valid cross-site results for long/variant queries.
        var filtered = scored
            .Where(x => x.Product.Price > 0 && x.Relevance > 0)
            .Where(x => queryTokens.Count <= 1 || x.Relevance >= 0.25m)
            .ToList();

        var normalized = (filtered.Count > 0 ? filtered : scored.Where(x => x.Product.Price > 0))
            .OrderByDescending(x => x.Relevance)
            .ThenBy(x => x.Product.Price)
            .GroupBy(x => x.Product.ProductUrl)
            .Select(x => x.First().Product)
            .ToList();

        if (normalized.Count == 0 && failedSources.Count == sources.Count)
        {
            throw new ValidationException("All configured sources failed for this query. Check saved links/sources and try again.");
        }

        return new ProductSearchResponseDto(query.Trim(), normalized);
    }

    private static List<string> BuildQueryTokens(string query)
    {
        var normalized = Regex.Replace(query.ToLowerInvariant(), @"[^a-z0-9]+", " ");
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length >= 2)
            .Where(x => !Regex.IsMatch(x, @"^\d+$")) // skip pure numbers (e.g. sizes/capacity) as strict relevance terms
            .Distinct()
            .ToList();
    }

    private static decimal CalculateRelevanceScore(string productName, IReadOnlyCollection<string> queryTokens)
    {
        if (queryTokens.Count == 0)
        {
            return 1m;
        }

        var normalizedName = productName.ToLowerInvariant();
        var matched = queryTokens.Count(token => normalizedName.Contains(token, StringComparison.OrdinalIgnoreCase));
        return (decimal)matched / queryTokens.Count;
    }
}
