using Aether.BuildingBlocks.Contracts.Authorization;
using Aether.BuildingBlocks.Contracts.ConfigCenter;
using Aether.BuildingBlocks.Contracts.I18n;
using Aether.BuildingBlocks.Contracts.Metadata;
using Aether.Modules.Authorization.Application;
using Aether.Modules.ConfigCenter.Application;
using Aether.Modules.I18n.Application;
using Aether.Modules.Metadata.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IFunctionalPermissionChecker, InMemoryFunctionalPermissionChecker>();
builder.Services.AddSingleton<IPolicyDecisionPoint, InMemoryPolicyDecisionPoint>();
builder.Services.AddSingleton<II18nProjectService, InMemoryI18nProjectService>();
builder.Services.AddSingleton<IDynamicQueryService, InMemoryDynamicQueryService>();
builder.Services.AddSingleton<IConfigOptionService>(sp =>
{
    var dynamicQuery = sp.GetRequiredService<IDynamicQueryService>();
    return new ConfigOptionService((req, ct) => dynamicQuery.ExecuteAsync(req, ct));
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseMiddleware<Aether.Host.AdminApi.GatewayAuthMiddleware>();

app.MapGet("/api/admin/i18n/projects", async (HttpContext ctx, II18nProjectService service) =>
{
    var tenantId = ctx.Items["TenantId"]!.ToString()!;
    var projects = await service.GetProjectsAsync(tenantId);
    return Results.Ok(new { code = "OK", data = projects });
}).WithMetadata(new Aether.Host.AdminApi.RequiredPermissionAttribute("i18n.project.manage"));

app.MapPost("/api/admin/i18n/projects", async (HttpContext ctx, II18nProjectService service, CreateI18nProjectCommand command) =>
{
    var tenantId = ctx.Items["TenantId"]!.ToString()!;
    var project = await service.CreateProjectAsync(tenantId, command);
    return Results.Ok(new { code = "OK", data = project });
}).WithMetadata(new Aether.Host.AdminApi.RequiredPermissionAttribute("i18n.project.manage"));

app.MapGet("/api/admin/config/options/{configKey}", async (HttpContext ctx, IConfigOptionService service, string configKey) =>
{
    var tenantId = ctx.Items["TenantId"]!.ToString()!;
    var options = await service.ResolveOptionsAsync(new ResolveConfigOptionsRequest(tenantId, configKey));
    return Results.Ok(new { code = "OK", data = options });
}).WithMetadata(new Aether.Host.AdminApi.RequiredPermissionAttribute("config.option.read"));

app.MapPost("/api/admin/authz/policies/evaluate", async (HttpContext ctx, IPolicyDecisionPoint pdp, PolicyEvaluateRequest req) =>
{
    var tenantId = ctx.Items["TenantId"]!.ToString()!;
    var userId = ctx.Items["UserId"]!.ToString()!;
    var input = req with { TenantId = tenantId, UserId = userId };
    var result = await pdp.EvaluateAsync(input);
    return Results.Ok(new { code = "OK", data = result });
}).WithMetadata(new Aether.Host.AdminApi.RequiredPermissionAttribute("authz.policy.evaluate"));

app.Run();
