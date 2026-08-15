---
type: adr
tags: [cordis-csharp, adr, configuration, providers]
created: 2026-08-15
status: accepted
id: adr-0013
---

# ADR-0013：配置提供者抽象（Keystone.Config）——解绑配置来源

## 背景

框架的配置来源不能锁死：不同宿主需要不同配置渠道（本地文件、配置中心、环境变量、云参数服务）。
P0 落地配置抽象（`src/Keystone.Config`），目标：

1. **提供者抽象**：任何配置来源可实现为提供者接入，用户可自实现
2. **默认双源**：本地 YAML（`cordis.yml`）+ AgileConfig 配置中心
3. **禁止硬编码**：运行期可调值一律经配置获取（KeystoneSettings 绑定，缺失时文档化默认值兜底）

## 决策

1. **抽象基于 `Microsoft.Extensions.Configuration` 的 `IConfigurationSource`/`IConfigurationProvider`**
   ——不重造配置（README 定位：不重造 IOptions），复用 .NET 标准提供者契约与生态（环境变量/JSON/命令行等既有源直接可用）
2. **内置两个提供者**：
   - `YamlFileConfigurationProvider`：YAML 解析经 **YamlStream 节点树**（纯解析无反射，规则 0 AOT 安全——反射反序列化器触发 IL3050 被排除）；支持嵌套/序列、锚点别名、merge keys（`<<`）；文件变更防抖热重载（doc 08 §6.3 写回防抖语义）
   - `AgileConfigConfigurationProvider`：包装官方 `AgileConfig.Client`（经 `IAgileConfigClient` 适配层隔离，可测试/可替换）；websocket 推送 → 重载；optional 未配置时跳过不阻塞启动
3. **优先级**（M.E.C 后添加者覆盖）：AgileConfig 配置中心 > 本地 YAML > 代码内默认值
4. **用户自定义提供者路径**：实现 `IConfigurationSource`/`IConfigurationProvider` + `Add...()` 扩展方法（`Keystone.Config.ConfigurationBuilderExtensions` 为示范）
5. **入口**：`KeystoneConfigBuilder`（fluent）+ `CreateDefault()`（YAML + AgileConfig）；或直接用 `IConfigurationBuilder` 扩展
6. **禁止硬编码落地**：`KeystoneSettings`（`cordis` 配置节）承载框架可调值（插件目录/超时/并发/日志级别），配置缺失用文档化默认值兜底；业务代码不内嵌魔法值

## 理由

1. **解绑来源**：配置锁死问题的根治 = 提供者抽象 + 标准契约；换来源只换 Add 调用
2. **不重造**：M.E.C 是 .NET 标准配置抽象，生态成熟（Azure App Config/Consul 等社区提供者可直接复用）
3. **AOT 安全**：YAML 走节点树解析（P0 验证：`dotnet publish -r osx-arm64 -p:PublishAot=true` 零 IL 告警）；AgileConfig 经适配层隔离
4. **可测试**：`IAgileConfigClient` 适配层使配置中心逻辑可 mock（P0 测试 15/15 绿）
5. **默认双源符合实用**：本地 YAML 保证离线可跑，AgileConfig 保证集中管理（覆盖本地）

## 权衡与风险

- **AgileConfig 官方集成未用**：官方 `AddAgileConfig` 的推送/重载语义不可控，故用自实现提供者 + 适配层（风险可控，官方 API 变化只影响适配器）
- **YamlDotNet 反射反序列化器排除**：`Deserialize<T>` 的动态类型绑定不可用（IL3050）；条目树结构化解析（P6）走节点树手动映射（本 ADR 已立此原则）
- **配置中心网络依赖**：optional 语义——未配置/连不上时 fail-open（空配置 + 默认值），不阻塞启动；启动后推送失败保持旧数据（"最后好数据保持"）

## 备选方案

1. **自研配置抽象接口**（ICordisConfigProvider）——被否决：重复 M.E.C 轮子，生态断裂
2. **只支持 YAML 文件**——被否决：配置来源多样性诉求不满足（用户明确要求配置中心）
3. **YamlDotNet 反射反序列化器 + IL3050 豁免**——被否决：违反规则 0（AOT 冒烟必须过）

## 影响

- 08-configuration-layer §2：配置形态补"提供者抽象 + 默认双源 + 优先级"（本 ADR 指针）
- 13-implementation-plan P0：配置抽象前置落地（原 P6 配置层的提供者部分提前）
- 11-gap-register：N 系列配置相关项落点明确为 Keystone.Config
- 规则 0：配置层 YAML 解析的 AOT 安全路径确立（YamlStream 节点树）

## 关联

- 规则 0（AGENTS.md）、ADR-0002（JIT/Roslyn 例外范围）、ADR-0011/0012（配置表达式边界与静态插值）
- 08-configuration-layer §2/§3/§6、13-implementation-plan P0/P6、14-implementation-log W0-04
