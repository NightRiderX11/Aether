using Aether.BuildingBlocks.Contracts.Authorization;
using Aether.Modules.Authorization.Application;

namespace Aether.Host.AdminApi;

public sealed class GatewayAuthMiddleware
{
    private readonly RequestDelegate _next;

    public GatewayAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IFunctionalPermissionChecker checker)
    {
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var userId = context.Request.Headers["X-User-Id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { code = "UNAUTHORIZED", message = "Missing gateway identity headers." });
            return;
        }

        // 网关仅做功能权限校验，细粒度 ABAC 不在此处理
        var requiredPermission = context.GetEndpoint()?.Metadata.GetMetadata<RequiredPermissionAttribute>()?.Code;
        if (!string.IsNullOrWhiteSpace(requiredPermission))
        {
            var allowed = await checker.HasPermissionAsync(new PermissionCheckRequest(tenantId, userId, requiredPermission));
            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { code = "FORBIDDEN", message = "Missing functional permission." });
                return;
            }
        }

        context.Items["TenantId"] = tenantId;
        context.Items["UserId"] = userId;
        await _next(context);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiredPermissionAttribute : Attribute
{
    public string Code { get; }

    public RequiredPermissionAttribute(string code)
    {
        Code = code;
    }
}
