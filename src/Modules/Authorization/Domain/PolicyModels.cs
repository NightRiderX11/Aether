namespace Aether.Modules.Authorization.Domain;

public enum PolicyEffect
{
    Allow,
    Deny
}

public sealed record PolicyDefinition(
    string Id,
    string TenantId,
    int Priority,
    PolicyEffect Effect,
    string Action,
    string ResourceType,
    Func<PolicyContext, bool> Predicate,
    string? DataFilterExpression);

public sealed record PolicyContext(
    string TenantId,
    string UserId,
    string Action,
    string ResourceType,
    IReadOnlyDictionary<string, object> Resource,
    IReadOnlyDictionary<string, object> Environment,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> OrgUnits);
