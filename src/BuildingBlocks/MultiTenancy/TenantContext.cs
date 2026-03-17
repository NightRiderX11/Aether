namespace Aether.BuildingBlocks.MultiTenancy;

public interface ITenantContext
{
    string TenantId { get; }
}

public sealed class TenantContext : ITenantContext
{
    public required string TenantId { get; init; }
}
