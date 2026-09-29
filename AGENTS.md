# AGENTS.md — Arona

## 项目概要

.NET 10 Blazor Interactive Server SSR 应用，AI Agent 对话系统。
- ORM: EF Core + SQL Server（含 `vector` 列和 FULLTEXT 索引）
- UI: Microsoft FluentUI.AspNetCore.Components 5.0
- AI: Microsoft.Agents.AI + OpenAI SDK（Chat Completions API，非 Responses API）
- 解决方案文件: `Arona.slnx`，单项目 `Arona/Arona.csproj`

## 构建与运行

```bash
dotnet build Arona.slnx -c Debug
dotnet run --project Arona
```

构建前确保 `Arona.exe` 未被占用（运行中会锁文件）。

## 架构要点

- `Program.cs`: DI 注册。`AIModels` 和 `AgentService` 均为 **Scoped**（消费 Scoped 的 `MainDbContext`）。`Config` 为 Singleton。
- `Services/AgentService.cs`: 创建 `ArAgent`，用 `GetChatClient` + `AsIChatClient()` + `ChatClientExtensions.AsAIAgent`（**不要用 ResponsesClient**，华为云 endpoint 不支持 `/responses`）。
- `Services/ArAgent.cs`: `DelegatingAIAgent` 包装器，override `RunCoreAsync` 实现滑动窗口（读 `Configs` 表 `ContextLength`）+ 历史持久化 + embedding 计算。
- `Services/FactTools.cs`: Fact 的增删查工具（向量 + 全文），`ScopeFilter` 默认只查全局 + 本会话 Fact。
- `Services/AgentTools.cs`: `SendMessageToUser` 工具，Agent 主输出通道。

## 关键约束

- **Agent 主输出走工具调用***（`SendMessageToUser`），不是文本。文本输出仅排障，不存库，`LogInformation` 输出到控制台。
- **排障文本不存库**。只有 user 消息和 `SendMessageToUser` 工具调用消息持久化到 `ChatMessages`。
- **`ChatMessages.Embedding` 列不允许 NULL**。用零向量 `new SqlVector<float>(new float[dimension])` 而非 `CreateNull`。
- **`AsIChatClient`** 是 `OpenAI.Chat.ChatClient` → `IChatClient` 的扩展方法（`Microsoft.Extensions.AI` 命名空间）。`OpenAIChatClient` 是 internal，不能直接 new。
- `AsAIAgent` 需同时 `using Microsoft.Agents.AI;` 和 `using Azure.AI.Projects;` 才能解析 `ResponsesClient` 重载（如需用 Responses API 的话）。

## FluentUI 5.0 注意事项

- 输入组件是 `FluentTextInput`（非 `FluentTextField`），多行用 `FluentTextArea`
- `FluentButton` 的 `Appearance` 参数类型是 `ButtonAppearance`（枚举值: Default/Outline/Primary/Subtle/Transparent），**没有 Accent**
- `FluentCheckbox` 用 `CheckState`/`Value` 体系，`@bind-Checked` 无效。用原生 `<input type="checkbox" @bind="..." />` 代替
- FluentUI 的 `default-fuib.css` 设置了 `body { height: 100dvh; overflow: hidden; }`，需在 `app.css` 里 `!important` 覆盖

## 数据库

- 连接字符串在 `appsettings.json` 的 `AronaConfig:MainDb`
- `MainDbContext.EnsureCreated()` 仅在首次建库时创建全文索引；已有库需手动 `CREATE FULLTEXT INDEX`
- `Configs` 表已知键: `Prompt`（系统提示词）、`ContextLength`（滑动窗口大小）、`ChatModel`（`{ProviderId}:{ModelName}`）、`EmbeddingModel`（同前）
- `Fact.SessionId` 为 null = 全局 Fact，非 null = 会话专属
- schema 变更用 migration，不要 Drop/Create

## 配置数据（华为云 ModelArts MAAS）

- Endpoint: `https://api.modelarts-maas.com/openai/v1`（不带区域前缀）
- 认证: `Authorization: Bearer {ApiKey}`（标准 Bearer）
- `ApiProvider.ApiKey` → `ChatModel.ModelEndpoint` → `ChatModel.ModelId` 三者需匹配