namespace Aether.BuildingBlocks.Contracts.ConfigCenter;

public sealed record ConfigOptionDto(string Label, string Value);
public sealed record ResolveConfigOptionsRequest(string TenantId, string ConfigKey);
