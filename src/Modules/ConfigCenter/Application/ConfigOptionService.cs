using Aether.BuildingBlocks.Contracts.ConfigCenter;
using Aether.BuildingBlocks.Contracts.Metadata;

namespace Aether.Modules.ConfigCenter.Application;

public interface IConfigOptionService
{
    Task<IReadOnlyCollection<ConfigOptionDto>> ResolveOptionsAsync(ResolveConfigOptionsRequest request, CancellationToken cancellationToken = default);
}

public sealed class ConfigOptionService : IConfigOptionService
{
    private readonly Func<DynamicQueryRequest, CancellationToken, Task<IReadOnlyCollection<DynamicQueryRow>>> _dynamicQuery;

    public ConfigOptionService(Func<DynamicQueryRequest, CancellationToken, Task<IReadOnlyCollection<DynamicQueryRow>>> dynamicQuery)
    {
        _dynamicQuery = dynamicQuery;
    }

    public async Task<IReadOnlyCollection<ConfigOptionDto>> ResolveOptionsAsync(ResolveConfigOptionsRequest request, CancellationToken cancellationToken = default)
    {
        // 演示：配置 key 对应动态查询源（实际应来自 config_option_source 表）
        var sourceRef = request.ConfigKey switch
        {
            "user.defaultOrgUnit" => "org.units",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(sourceRef))
        {
            return Array.Empty<ConfigOptionDto>();
        }

        var rows = await _dynamicQuery(new DynamicQueryRequest(request.TenantId, sourceRef), cancellationToken);
        return rows
            .Select(x => new ConfigOptionDto(
                Label: x.Fields.GetValueOrDefault("name")?.ToString() ?? string.Empty,
                Value: x.Fields.GetValueOrDefault("id")?.ToString() ?? string.Empty))
            .ToArray();
    }
}
