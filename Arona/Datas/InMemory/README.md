# InMemory

运行时内存模型（非持久化），从数据库加载后供服务层使用。

## 文件

- `AIModels.cs` — 当前选中的聊天/嵌入模型持有者。从 `Configs` 表读取 `ChatModel`/`EmbeddingModel` 键（格式 `{ProviderId}:{ModelName}`），解析出对应的 `MemoryChatModel`/`MemoryEmbeddingModel`（含 ApiKey、Endpoint）。提供 `Reload` / `SaveToDb`。
- `MemoryChatModel.cs` — 聊天模型运行时视图：`Id`、`ModelId`、`Name`、`Endpoint`、`ApiKey`。
- `MemoryEmbeddingModel.cs` — 嵌入模型运行时视图：上述字段外加 `Dimensions`、`IsFixedDimension`。

## 约定

- `AIModels` 为 Scoped（依赖 `MainDbContext`、`Config`）。
- 嵌入模型 `Dimensions` 必须与 `Config.EmbeddingDimension` 一致，否则抛 `InvalidOperationException`。