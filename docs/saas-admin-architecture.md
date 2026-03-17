# 多租户通用后台管理系统总体架构设计（ASP.NET Core 10）

> 目标：在同一套平台中同时支持「平台运营后台」与「租户后台」，以模块化单体（Modular Monolith）实现高复用，并预留未来拆分为微服务的演进路径。

---

## 1. 总体架构与分层

### 1.1 架构原则

1. **单体优先、边界清晰**：先用模块化单体降低复杂度，通过模块边界（接口/事件）避免耦合。
2. **租户优先设计**：除平台级数据外，所有业务数据默认带 `TenantId`。
3. **权限策略外置化**：RBAC 做“粗粒度分配”，ABAC 做“细粒度裁决”，策略由 UI 配置并可热更新。
4. **可观测与审计内建**：审计日志、链路追踪、操作人/租户上下文从第一天纳入。
5. **可演进性**：通过 Outbox/EventBus、模块 API 合约、独立 schema/table 预留拆分空间。

### 1.2 分层结构（后端）

- **Presentation 层**：REST API（Controller/Minimal API），BFF 可选。
- **Application 层**：用例编排、DTO、事务边界、权限校验入口。
- **Domain 层**：聚合、领域服务、规则对象（角色、策略、组织）。
- **Infrastructure 层**：EF Core/MySQL、Redis、RabbitMQ、网关鉴权集成。
- **Shared Kernel**：租户上下文、审计上下文、事件基类、异常规范。

建议目录：

```text
src/
  BuildingBlocks/
    MultiTenancy/
    Security/
    Auditing/
    Localization/
    DynamicQuery/
  Modules/
    Identity/
    Authorization/
    Organization/
    I18n/
    ConfigCenter/
    AuditLog/
    Metadata/
  Host/AdminApi/
```

---

## 2. 模块划分与依赖关系

### 2.1 核心模块

1. **Identity（认证与用户）**
   - 登录/登出、Session、Token、用户 CRUD、密码与安全策略。
2. **Authorization（权限）**
   - RBAC（角色、菜单/资源、角色授权）
   - ABAC（策略、策略表达式、执行引擎）
3. **Organization（组织架构）**
   - 部门、岗位、汇报关系、用户组织归属。
4. **Metadata（元数据）**
   - ABAC 属性模型、资源模型、动态查询字段定义。
5. **I18n（多语言文案）**
   - 语言包、文案 Key、翻译版本、发布。
6. **ConfigCenter（业务配置中心）**
   - 分层配置（平台级/租户级/环境级）、版本与灰度。
7. **AuditLog（审计）**
   - 登录日志、操作日志、策略命中日志。
8. **GatewayIntegration（网关集成）**
   - 网关 JWT 校验、策略前置判定缓存、服务发现/路由元数据。

### 2.2 依赖约束（强约束）

- **所有业务模块禁止相互依赖**（禁止 `ModuleA -> ModuleB` 的项目引用）。
- 模块通信必须通过专门抽象层：
  - `BuildingBlocks/Contracts`（查询接口、命令接口、DTO 合约）
  - `BuildingBlocks/Events`（集成事件）
  - `BuildingBlocks/Integration`（统一调用网关/消息分发器）
- **禁止模块互相直连数据库表**，跨模块数据获取只能走抽象接口或订阅事件后构建本地只读模型。
- `AuditLog`、`ConfigCenter`、`I18n` 仅暴露抽象接口给其他模块消费，不允许反向业务依赖。

建议增加目录：

```text
src/
  BuildingBlocks/
    Contracts/
      Identity/
      Authorization/
      Organization/
      I18n/
      ConfigCenter/
```

### 2.3 模块通信

- **同步**：模块内部接口（同进程）
- **异步**：领域事件 + Outbox + RabbitMQ（如“用户禁用”触发会话失效）

---

## 3. 数据库表结构设计（MySQL）

> 统一字段建议：
> `Id (bigint/char(26))`, `TenantId`, `IsDeleted`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `RowVersion`。

### 3.1 租户与基础

- `tenant`
  - `id`, `code`, `name`, `status`, `plan_code`, `expired_at`
- `tenant_feature`
  - `id`, `tenant_id`, `feature_key`, `enabled`, `config_json`
- `tenant_domain`
  - `id`, `tenant_id`, `domain`, `is_primary`

### 3.2 用户与认证

- `user`
  - `id`, `tenant_id`, `username`, `email`, `phone`, `password_hash`, `status`, `last_login_at`
- `user_profile`
  - `user_id`, `display_name`, `avatar_url`, `locale`, `timezone`
- `credential`
  - `id`, `user_id`, `type`(password/mfa/oauth), `secret_hash`, `expires_at`
- `session`
  - `id`, `tenant_id`, `user_id`, `token_jti`, `issued_at`, `expires_at`, `revoked_at`, `ip`, `ua`
- `login_log`
  - `id`, `tenant_id`, `user_id`, `result`, `reason`, `ip`, `ua`, `created_at`

索引示例：
- `uk_user_tenant_username (tenant_id, username)`
- `idx_session_tenant_user (tenant_id, user_id, expires_at)`

### 3.3 RBAC

- `role`
  - `id`, `tenant_id`, `code`, `name`, `scope`(platform/tenant), `is_builtin`
- `permission`
  - `id`, `code`, `name`, `resource_type`, `action`, `module`
- `role_permission`
  - `role_id`, `permission_id`
- `user_role`
  - `tenant_id`, `user_id`, `role_id`

### 3.4 ABAC 与策略

- `policy`
  - `id`, `tenant_id`, `code`, `name`, `effect`(allow/deny), `priority`, `status`, `version`
- `policy_target`
  - `id`, `policy_id`, `subject_type`(user/role/org), `subject_ref`
- `policy_rule`
  - `id`, `policy_id`, `rule_json`(表达式 AST), `description`
- `policy_publish`
  - `id`, `tenant_id`, `version`, `published_by`, `published_at`
- `policy_decision_log`
  - `id`, `tenant_id`, `trace_id`, `subject_id`, `resource`, `action`, `decision`, `matched_policy_ids`, `context_json`

### 3.5 组织架构

- `org_unit`
  - `id`, `tenant_id`, `parent_id`, `type`(dept/team), `code`, `name`, `path`, `level`
- `position`
  - `id`, `tenant_id`, `code`, `name`
- `user_org`
  - `tenant_id`, `user_id`, `org_unit_id`, `position_id`, `is_primary`

### 3.6 国际化（含项目管理）

- `i18n_project`
  - `id`, `tenant_id`(可空), `project_code`, `project_name`, `default_locale`, `status`
- `i18n_project_locale`
  - `id`, `project_id`, `locale`, `is_enabled`, `fallback_locale`
- `i18n_key`
  - `id`, `tenant_id`(可空，空=平台级), `project_id`, `namespace`, `key`, `description`
- `i18n_text`
  - `id`, `key_id`, `locale`, `text`, `version`, `status`

说明：一个平台可维护多个项目（如 `tenant-portal`、`ops-console`、`mobile-app`），通过 `project_id` 隔离词条空间。

### 3.7 配置中心（含动态选项）

- `config_item`
  - `id`, `tenant_id`(可空), `scope`(platform/tenant/env), `config_key`, `value_json`, `version`, `status`
- `config_publish`
  - `id`, `scope`, `target_ref`, `version`, `published_at`, `published_by`
- `config_option_source`
  - `id`, `tenant_id`(可空), `config_key`, `source_type`(static/dynamic_query/api), `source_ref`, `mapping_json`, `cache_ttl_sec`

说明：当配置项是下拉选择器时，`source_type=dynamic_query`，通过 `source_ref` 指向 `meta_entity` 或预定义查询模板，运行时拉取动态数据并映射为 `label/value`。

### 3.8 审计

- `audit_log`
  - `id`, `tenant_id`, `operator_id`, `module`, `action`, `resource_type`, `resource_id`, `changes_json`, `ip`, `ua`, `created_at`

### 3.9 元数据与动态查询

- `meta_entity`
  - `id`, `tenant_id`(可空), `entity_code`, `entity_name`, `source_type`(table/view/api)
- `meta_field`
  - `id`, `entity_id`, `field_code`, `field_name`, `data_type`, `operators_json`, `is_indexed`, `path_expr`
- `meta_relation`
  - `id`, `entity_id`, `related_entity_id`, `join_expr`

---

## 4. ABAC + RBAC 混合模型实现方案

### 4.1 判定模型（推荐）

1. **先 RBAC 后 ABAC**：
   - RBAC 判定是否拥有基础权限（如 `Order.Read`）。
   - ABAC 在此基础上做数据域过滤/拒绝（如“仅可访问本部门订单”）。
2. **Deny 优先**：同优先级下 `deny` 覆盖 `allow`。
3. **优先级**：策略按 `priority` 排序，支持短路。
4. **默认拒绝**：无匹配策略则拒绝。

### 4.2 请求上下文（PDP 输入）

- `subject`：用户ID、角色、部门、岗位、租户、标签
- `resource`：资源类型、资源ID、所属租户、业务属性
- `action`：read/create/update/delete/approve...
- `environment`：时间/IP/设备/渠道

### 4.3 架构角色

- **PAP**（Policy Admin Point）：策略管理 UI + 发布流程
- **PDP**（Policy Decision Point）：策略决策引擎（内存缓存+解释执行）
- **PEP**（Policy Enforcement Point）：API 网关 + 应用服务过滤器
- **PIP**（Policy Information Point）：元数据服务 + 动态查询服务

### 4.4 数据权限落地

- 对列表查询，ABAC 输出 **数据过滤表达式**（SQL where 片段的安全 AST）
- 通过 EF Core Expression Tree 注入全局过滤器/仓储查询。
- 对单条资源访问，执行 point-check（`CanAccess(resourceId)`）。

---

## 5. 策略表达式引擎设计

### 5.1 表达式 DSL（UI 可配置）

采用 JSON AST（而非自由文本）降低注入风险。

示例：

```json
{
  "op": "and",
  "items": [
    {"op": "eq", "left": {"var": "resource.tenantId"}, "right": {"var": "subject.tenantId"}},
    {"op": "in", "left": {"var": "resource.orgUnitId"}, "right": {"var": "subject.orgUnits"}},
    {"op": "neq", "left": {"var": "resource.status"}, "right": "Archived"}
  ]
}
```

### 5.2 引擎流程

1. 策略拉取（按租户 + 版本）
2. 语义校验（字段存在、类型匹配、操作符合法）
3. 编译为可执行计划（内存对象/Expression Tree）
4. 执行并返回：
   - `decision`：allow/deny/not_applicable
   - `dataFilter`：可下推到数据库的过滤 AST
   - `obligations`：脱敏/水印/字段隐藏等义务动作

### 5.3 元数据与动态查询支撑

- UI 配策略时，根据 `meta_entity/meta_field` 动态展示可选字段与操作符。
- PIP 负责把 `subject`、`resource` 属性补齐（如用户部门树、岗位等级、资源标签）。
- 对不可下推数据库的规则，在应用层二次过滤并记录性能指标。

### 5.4 性能与缓存

- Redis 缓存：`policy_bundle:{tenant}:{version}`
- 本地缓存 + 版本戳（发布后消息通知失效）
- 高频策略可预编译并保存在内存字典中。

---

## 6. 多租户隔离方案

### 6.1 隔离模式建议（分阶段）

- **阶段1（默认）**：共享库共享表 + `TenantId` 行级隔离（开发效率高）
- **阶段2（高价值租户）**：共享库独立 Schema
- **阶段3（超大租户）**：独立数据库（通过连接字符串路由）

### 6.2 租户识别

优先级建议：
1. 子域名（`tenantA.admin.example.com`）
2. 请求头（`X-Tenant-Id`，仅内网可信）
3. Token Claim（`tid`）

必须做一致性校验：请求头租户、子域租户、Token 租户不一致即拒绝。

### 6.3 数据安全机制

- EF Core 全局查询过滤器强制 `TenantId`
- 写入拦截器自动注入 `TenantId`
- 平台管理员访问租户数据需“显式切换上下文 + 审计记录”
- 禁止跨租户 JOIN（由仓储层规则拦截）

---

## 7. API 网关集成方案

### 7.1 网关职责

- 统一认证（JWT/OIDC）
- 限流、黑白名单、WAF
- 透传上下文：`X-User-Id`, `X-Tenant-Id`, `X-Trace-Id`, `X-Roles`
- 部分粗粒度授权前置（例如菜单级）

### 7.2 与权限系统协同

- **固定方案（按本项目要求）**：网关仅做认证与功能权限（菜单/API action）校验。
- 细粒度 ABAC（数据范围、字段脱敏、上下文约束）统一由业务服务内部 PDP 判定，网关不承载 ABAC 逻辑。

### 7.3 事件联动

- 用户禁用/角色变更/策略发布 -> RabbitMQ 广播 -> 网关与服务清理本地权限缓存。

---

## 8. 前后端交互接口规范

### 8.0 前后端分离部署规范

- 前端应用（SPA）与后端 API 独立仓库/独立流水线发布。
- 前端通过 API 网关访问后端，不直连业务服务。
- 后端仅返回 JSON API，不在服务端渲染页面。
- 建议：
  - Frontend: `admin-web`（React/Vue 均可）
  - Backend: `admin-api`（ASP.NET Core 10）
  - Shared Contract: OpenAPI + TypeScript SDK 自动生成

### 8.1 统一 API 规范

- Base URL：`/api/admin/{module}`
- Header：
  - `Authorization: Bearer <token>`
  - `X-Tenant-Id`
  - `X-Correlation-Id`
  - `Accept-Language`
- 响应信封：

```json
{
  "code": "OK",
  "message": "",
  "data": {},
  "traceId": "..."
}
```

### 8.2 关键接口建议

- 认证：
  - `POST /api/admin/identity/auth/login`
  - `POST /api/admin/identity/auth/logout`
  - `POST /api/admin/identity/auth/refresh`
- 用户：
  - `GET /api/admin/identity/users`
  - `POST /api/admin/identity/users`
  - `PUT /api/admin/identity/users/{id}`
- 角色权限：
  - `GET /api/admin/authz/roles`
  - `POST /api/admin/authz/roles/{id}/permissions`
- 策略：
  - `GET /api/admin/authz/policies`
  - `POST /api/admin/authz/policies`
  - `POST /api/admin/authz/policies/{id}/simulate`
  - `POST /api/admin/authz/policies/publish`
- 元数据：
  - `GET /api/admin/metadata/entities`
  - `GET /api/admin/metadata/entities/{code}/fields`

### 8.3 前端权限集成

- 登录后获取 `permission snapshot`（RBAC）+ `data scope hints`（ABAC 摘要）。
- 路由守卫只做 UX 控制，后端必须二次校验。
- 策略编辑器：左侧实体字段树（元数据），中间条件编排，右侧实时模拟结果。

---

## 9. 非功能设计建议

1. **可观测性**：OpenTelemetry + Prometheus + Loki/ELK。
2. **审计合规**：关键操作双写审计（业务日志 + 审计库），支持按租户导出。
3. **一致性**：本地事务 + Outbox，避免分布式事务。
4. **灰度发布**：策略版本与配置版本均支持回滚。
5. **安全**：敏感字段加密（如手机号）、密钥托管、租户级数据导出审批。

---

## 10. 分阶段落地路线图（建议）

### Phase 1（MVP）
- 租户、用户、角色权限（RBAC）
- 登录/会话/审计
- 组织架构基础
- 配置中心基础

### Phase 2
- ABAC 策略引擎（JSON AST）
- 元数据管理 + 策略 UI
- 数据权限下推（查询过滤）

### Phase 3
- 网关增强鉴权
- 多语言发布流程
- 高价值租户隔离升级（Schema/DB）

---

## 11. 需要你补充确认的关键问题（下一步）

1. **租户规模预估**：租户数、单租户用户量、峰值 QPS？
2. **权限复杂度**：是否存在字段级脱敏、行列级混合权限？
3. **合规要求**：是否需满足等保/ISO/GDPR（影响审计和数据保留策略）？
4. **SSO 需求**：是否要对接企业微信、Azure AD、OIDC IdP？
5. **网关选型**：Kong、APISIX、YARP 还是云厂商网关？
6. **前端框架偏好**：React/Vue/Angular（影响策略编辑器实现方案）？

如果你愿意，我下一步可以直接给出：
- 一份 **可执行的数据库 DDL 初稿**（MySQL 8）
- 一份 **ASP.NET Core 模块脚手架结构**
- 一份 **ABAC 策略引擎核心接口与伪代码**（含策略模拟 API）
