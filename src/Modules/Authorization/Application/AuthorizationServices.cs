using Aether.BuildingBlocks.Contracts.Authorization;
using Aether.Modules.Authorization.Domain;

namespace Aether.Modules.Authorization.Application;

public interface IFunctionalPermissionChecker
{
    Task<bool> HasPermissionAsync(PermissionCheckRequest request, CancellationToken cancellationToken = default);
}

public interface IPolicyDecisionPoint
{
    Task<PolicyEvaluateResult> EvaluateAsync(PolicyEvaluateRequest request, CancellationToken cancellationToken = default);
}

public sealed class InMemoryFunctionalPermissionChecker : IFunctionalPermissionChecker
{
    private readonly Dictionary<string, HashSet<string>> _userPermissions = new();

    public InMemoryFunctionalPermissionChecker()
    {
        _userPermissions["u-admin"] = new HashSet<string> { "identity.user.read", "authz.policy.evaluate", "i18n.project.manage", "config.option.read" };
    }

    public Task<bool> HasPermissionAsync(PermissionCheckRequest request, CancellationToken cancellationToken = default)
    {
        var key = request.UserId;
        var ok = _userPermissions.TryGetValue(key, out var permissions) && permissions.Contains(request.PermissionCode);
        return Task.FromResult(ok);
    }
}

public sealed class InMemoryPolicyDecisionPoint : IPolicyDecisionPoint
{
    private readonly List<PolicyDefinition> _policies;

    public InMemoryPolicyDecisionPoint()
    {
        _policies = new()
        {
            new PolicyDefinition(
                Id: "deny-cross-tenant",
                TenantId: "*",
                Priority: 1,
                Effect: PolicyEffect.Deny,
                Action: "*",
                ResourceType: "*",
                Predicate: ctx => ctx.Resource.TryGetValue("tenantId", out var tid) && tid?.ToString() != ctx.TenantId,
                DataFilterExpression: null),
            new PolicyDefinition(
                Id: "allow-self-tenant-read",
                TenantId: "*",
                Priority: 10,
                Effect: PolicyEffect.Allow,
                Action: "read",
                ResourceType: "*",
                Predicate: ctx => true,
                DataFilterExpression: "resource.tenantId == subject.tenantId")
        };
    }

    public Task<PolicyEvaluateResult> EvaluateAsync(PolicyEvaluateRequest request, CancellationToken cancellationToken = default)
    {
        var context = new PolicyContext(
            request.TenantId,
            request.UserId,
            request.Action,
            request.ResourceType,
            request.Resource,
            request.Environment,
            Roles: new[] { "admin" },
            OrgUnits: new[] { "root" });

        var matches = _policies
            .Where(x => (x.TenantId == "*" || x.TenantId == request.TenantId)
                     && (x.Action == "*" || x.Action == request.Action)
                     && (x.ResourceType == "*" || x.ResourceType == request.ResourceType)
                     && x.Predicate(context))
            .OrderBy(x => x.Priority)
            .ToList();

        if (matches.Count == 0)
        {
            return Task.FromResult(new PolicyEvaluateResult("deny", null, Array.Empty<string>()));
        }

        var firstDeny = matches.FirstOrDefault(x => x.Effect == PolicyEffect.Deny);
        if (firstDeny is not null)
        {
            return Task.FromResult(new PolicyEvaluateResult("deny", null, new[] { firstDeny.Id }));
        }

        var firstAllow = matches.First(x => x.Effect == PolicyEffect.Allow);
        return Task.FromResult(new PolicyEvaluateResult("allow", firstAllow.DataFilterExpression, matches.Select(x => x.Id).ToArray()));
    }
}
