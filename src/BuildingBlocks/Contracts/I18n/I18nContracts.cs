namespace Aether.BuildingBlocks.Contracts.I18n;

public sealed record I18nProjectDto(string Id, string ProjectCode, string ProjectName, string DefaultLocale, string Status);
public sealed record CreateI18nProjectCommand(string ProjectCode, string ProjectName, string DefaultLocale);
