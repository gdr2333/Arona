# Datas

数据模型根目录。分为持久化（EF Core）与内存（运行时）两层。

## 文件

- `Config.cs` — 应用配置 POCO，由 `AronaConfig` 配置段绑定。含 `MainDb`、`AdminPassword`、`EmbeddingDimension`、`OnebotListenUri`、`OnebotAccessToken`。

## 子目录

- [`InMemory/`](InMemory/README.md) — 运行时内存模型
- [`Storage/`](Storage/README.md) — EF Core 持久化模型与 `MainDbContext`