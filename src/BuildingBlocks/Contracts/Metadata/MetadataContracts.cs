namespace Aether.BuildingBlocks.Contracts.Metadata;

public sealed record DynamicQueryRequest(string TenantId, string SourceRef);
public sealed record DynamicQueryRow(Dictionary<string, object> Fields);
