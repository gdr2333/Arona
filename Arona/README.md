# Arona

.NET 10 Blazor Interactive Server SSR 应用，AI Agent 对话系统，已接入 OnebotV11 反向 WebSocket。

## 关键文件

- `Program.cs` — DI 注册与中间件管道。`AIModels`/`AgentService` 为 Scoped（消费 Scoped 的 `MainDbContext`），`Config`/`GroupBlacklistService` 为 Singleton，`OnebotService` 为 HostedService。
- `Arona.csproj` — 单项目，目标 `net10.0`。
- `appsettings.json` / `appsettings.Development.json` — 配置（`AronaConfig` 段：数据库连接、嵌入维度、Onebot 监听地址等）。

## 子目录

- [`Components/`](Components/README.md) — Blazor UI 组件
- [`Datas/`](Datas/README.md) — 数据模型（持久化 + 内存）
- [`Services/`](Services/README.md) — 业务服务（Agent、Onebot、工具）

## 构建与运行

```bash
dotnet build Arona.slnx -c Debug
dotnet run --project Arona
```

构建前确保 `Arona.exe` 未被占用。`--cleardb` 参数可清库重建。