# Storage

EF Core 持久化模型与 `MainDbContext`，SQL Server（含 `vector` 列与 FULLTEXT 索引）。

## 文件

| 文件 | 实体 | 说明 |
|---|---|---|
| `MainDbContext.cs` | — | DbContext。`vector(dim)` 列类型、会话/模型外键映射；`EnsureCreated()` 首次建库时创建全文索引目录与 `Facts` 全文索引 |
| `ChatSession.cs` | `ChatSession` | 会话。`Id` 约定：`{botId}:private:{userId}` / `{botId}:group:{groupId}` / `webui:{guid}` |
| `ChatMessage.cs` | `ChatMessage` | 消息。`Role`、`Message`、`Embedding`（`SqlVector<float>`，非空）、`UtcTime` |
| `Fact.cs` | `Fact` | 事实/记忆。`SessionId` 为 null=全局，非 null=会话专属 |
| `ApiProvider.cs` | `ApiProvider` | API 供应商（含 `ApiKey`），下挂聊天/嵌入模型 |
| `ChatModel.cs` | `ChatModel` | 聊天模型，复合主键 `(ModelId, ProviderId)`，`ModelEndpoint` |
| `EmbeddingModel.cs` | `EmbeddingModel` | 嵌入模型，复合主键，`Dimensions`、`IsFixedDimension` |
| `StringConfig.cs` | `StringConfig` | 通用键值配置。已知键：`Prompt`、`ContextLength`、`ChatModel`、`EmbeddingModel`、`GroupBlacklist` |

## 约定

- `ChatMessages.Embedding` 不允许 NULL，用零向量 `new SqlVector<float>(new float[dim])` 而非 `CreateNull`。
- schema 变更走 migration，不要 Drop/Create。
- 全文索引依赖 `Facts(Content)` 的 `PK_Facts`。