namespace Aether.BuildingBlocks.Contracts.Authorization;

public sealed record PermissionCheckRequest(string TenantId, string UserId, string PermissionCode);
public sealed record PermissionCheckResult(bool Allowed);

public sealed record PolicyEvaluateRequest(
    string TenantId,
    string UserId,
    string Action,
    string ResourceType,
    Dictionary<string, object> Resource,
    Dictionary<string, object> Environment);

public sealed record PolicyEvaluateResult(string Decision, string? DataFilterExpression, IReadOnlyCollection<string> MatchedPolicies);
