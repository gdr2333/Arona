# Pages

路由页面（`@rendermode InteractiveServer`）。统一使用 FluentUI 组件与 `config-*` CSS 类。

## 页面

| 文件 | 路由 | 职责 |
|---|---|---|
| `Home.razor` | `/` | 首页卡片导航 |
| `Chat.razor` | `/chat` | WebUI 对话（会话侧栏 + 消息流 + 输入）。会话 ID 前缀 `webui:`，场景 `ChatScenario.WebUI` |
| `Config.razor` | `/config` | `Configs` 表键值配置管理（Prompt、ContextLength、ChatModel、EmbeddingModel 等） |
| `Providers.razor` | `/providers` | API 供应商（凭证）管理 |
| `Models.razor` | `/models` | 聊天模型与嵌入模型管理，复合主键 `(ModelId, ProviderId)` |
| `Blacklist.razor` | `/blacklist` | 群黑名单管理（增删查，经 `GroupBlacklistService` 持久化到 `Configs` 表） |
| `Error.razor` | — | 异常处理页 |
| `NotFound.razor` | — | 404 页 |

## 约定

- Agent 主回复从 `SendMessageToUser` 工具调用中提取（`FunctionCallContent`），文本输出仅作排障显示。
- 管理页面直接注入 `MainDbContext`（Scoped）进行 CRUD。