# Services

业务服务层。

## 文件

| 文件 | 类型 | 职责 |
|---|---|---|
| `AgentService.cs` | Scoped | 创建 `ArAgent`。用 `OpenAIClient` → `AsIChatClient` → `AsAIAgent`（非 Responses API）。按 `ChatScenario` 生成输出规则，挂载 `SendMessageToUser` 与 `FactTools` |
| `ArAgent.cs` | `DelegatingAIAgent` | Agent 包装器。滑动窗口（读 `Configs.ContextLength`）+ 历史持久化 + embedding 计算。主回复经 `SendMessageToUser` 工具调用入库，文本输出仅日志 |
| `AgentTools.cs` | 静态 | `SendMessageToUser` 工具定义与参数提取 |
| `FactTools.cs` | 实例 | Fact 增删查工具：向量检索（`VectorDistance` cosine）、全文检索（`Contains`）、历史检索。`ScopeFilter` 默认查全局+本会话 |
| `OnebotService.cs` | HostedService | OnebotV11 反向 WebSocket 端点。私聊逐条处理；群聊缓冲入队，每 30 秒批量合并注入。黑名单群忽略。消息带 `[昵称]: 内容` 前缀，按场景传 `ChatScenario` |
| `GroupBlacklistService.cs` | Singleton | 群黑名单。从 `Configs.GroupBlacklist`（逗号分隔）加载到内存，提供 `IsBlacklisted`/`AddAsync`/`RemoveAsync` |
| `ChatScenario.cs` | 枚举 | `WebUI` / `Private` / `Group`，驱动系统提示词分支 |

## 关键约束

- Agent 主输出走工具调用（`SendMessageToUser`），不是文本；排障文本不存库。
- `AsIChatClient` 是 `OpenAI.Chat.ChatClient` → `IChatClient` 的扩展（`Microsoft.Extensions.AI`）。华为云 endpoint 不支持 `/responses`，不要用 ResponsesClient。
- `OnebotService` 事件回调为单例级，通过 `IServiceScopeFactory` 创建 scope 获取 Scoped 的 `AgentService`；每会话 `SemaphoreSlim` 串行化。
- 群聊批量注入：`_groupBuffers`（per-group `ConcurrentQueue`）+ 后台 30s 刷新循环，群间并行、群内串行。