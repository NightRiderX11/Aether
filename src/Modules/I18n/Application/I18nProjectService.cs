using Aether.BuildingBlocks.Contracts.I18n;

namespace Aether.Modules.I18n.Application;

public interface II18nProjectService
{
    Task<I18nProjectDto> CreateProjectAsync(string tenantId, CreateI18nProjectCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<I18nProjectDto>> GetProjectsAsync(string tenantId, CancellationToken cancellationToken = default);
}

public sealed class InMemoryI18nProjectService : II18nProjectService
{
    private readonly Dictionary<string, List<I18nProjectDto>> _data = new();

    public Task<I18nProjectDto> CreateProjectAsync(string tenantId, CreateI18nProjectCommand command, CancellationToken cancellationToken = default)
    {
        if (!_data.ContainsKey(tenantId)) _data[tenantId] = new List<I18nProjectDto>();

        var project = new I18nProjectDto(
            Guid.NewGuid().ToString("N"),
            command.ProjectCode,
            command.ProjectName,
            command.DefaultLocale,
            "enabled");

        _data[tenantId].Add(project);
        return Task.FromResult(project);
    }

    public Task<IReadOnlyCollection<I18nProjectDto>> GetProjectsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (!_data.TryGetValue(tenantId, out var projects))
        {
            return Task.FromResult((IReadOnlyCollection<I18nProjectDto>)Array.Empty<I18nProjectDto>());
        }

        return Task.FromResult((IReadOnlyCollection<I18nProjectDto>)projects);
    }
}
