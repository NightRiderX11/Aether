using Aether.BuildingBlocks.Contracts.Metadata;

namespace Aether.Modules.Metadata.Application;

public interface IDynamicQueryService
{
    Task<IReadOnlyCollection<DynamicQueryRow>> ExecuteAsync(DynamicQueryRequest request, CancellationToken cancellationToken = default);
}

public sealed class InMemoryDynamicQueryService : IDynamicQueryService
{
    public Task<IReadOnlyCollection<DynamicQueryRow>> ExecuteAsync(DynamicQueryRequest request, CancellationToken cancellationToken = default)
    {
        var rows = request.SourceRef switch
        {
            "org.units" => new[]
            {
                new DynamicQueryRow(new Dictionary<string, object> { ["id"] = "dept-sales", ["name"] = "Sales" }),
                new DynamicQueryRow(new Dictionary<string, object> { ["id"] = "dept-rnd", ["name"] = "R&D" })
            },
            _ => Array.Empty<DynamicQueryRow>()
        };

        return Task.FromResult((IReadOnlyCollection<DynamicQueryRow>)rows);
    }
}
