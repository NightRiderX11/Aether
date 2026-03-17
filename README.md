# Aether Admin Backend Skeleton

该仓库已根据 `docs/saas-admin-architecture.md` 落地一个最小可运行的后端骨架（前后端分离）。

## 已实现

- 模块强隔离通信约束：通过 `BuildingBlocks/Contracts` 与服务抽象进行交互，不做模块直接引用。
- 网关职责边界：仅使用中间件执行身份头校验和功能权限校验。
- 细粒度 ABAC：由业务服务内 `IPolicyDecisionPoint` 执行策略判定。
- 多语言项目管理：`/api/admin/i18n/projects` 增删查（当前示例实现为内存存储）。
- 配置中心动态选项：`/api/admin/config/options/{configKey}` 基于动态查询返回下拉项。

## 运行（需要 .NET 10 SDK）

```bash
cd src/Host/AdminApi
dotnet run
```

## 示例请求头

- `X-Tenant-Id: t-demo`
- `X-User-Id: u-admin`

