# 开发代号 A.R.O.N.A.

（虽然名字是阿罗娜但实际上是个通用 AI Bot

启发自 [MaiBot](https://github.com/Mai-with-u/MaiBot) 项目，目标是一个简单但功能相对全面的 AI 机器人。

本项目使用 C#/.NET + EF Core + SQL Server 构建，~~作者认为这会很好玩~~

## 功能

- 自动滑动窗口（按 `Configs.ContextLength` 动态截断历史）
- 主动发言（Agent 有权决定是否回复）
- 长期记忆（Fact：向量检索 + 全文检索，支持全局/会话作用域）
- WebUI 对话界面（Blazor Interactive Server）
- OnebotV11 反向 WebSocket 接入
  - 私聊逐条处理
  - 群聊缓冲合并，每 30 秒批量注入
  - 群黑名单过滤
- 多 AI 供应商/模型管理（Chat + Embedding）

## 部署

目前没有 Release 文件，请从源码 Build 后部署。

### 源码构建

你需要：

- .NET 10 SDK
- SQL Server 2025（LocalDB/Express 均可，需支持 `vector` 类型）
- 最好是 Windows 环境，当然你能在 Linux 上给这俩东西弄明白我也没话说。

首先，构建 Arona 项目

```sh
dotnet publish Arona/Arona.csproj
```

然后，配置 `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AronaConfig": {
    "MainDb": "你的 SQL Server ADO.NET 连接字符串",
    "AdminPassword": "管理页面密码",
    "EmbeddingDimension": 1024,
    "OnebotListenUri": "http://localhost:8080/onebot/v11/ws/",
    "OnebotAccessToken": "反向 WS 认证 Token（可选）"
  },
  "AllowedHosts": "*"
}
```

| 字段 | 说明 |
|---|---|
| `MainDb` | SQL Server ADO.NET 连接字符串 |
| `AdminPassword` | 管理页面密码 |
| `EmbeddingDimension` | 向量维度（需与所选 Embedding 模型一致） |
| `OnebotListenUri` | OnebotV11 反向 WS 监听地址（`http://` 开头），留空则不启用 |
| `OnebotAccessToken` | 反向 WS 认证 Token（可选） |

然后启动，在 WebUI 配置中配置你的 AI 供应商与模型，就可以正常对话了。

```sh
dotnet run --project Arona
```

如需清库重建，加 `--cleardb` 参数（⚠️ 会删除所有数据）。

## 开发与贡献

开发环境与源码构建环境相同。详见 [AGENTS.md](AGENTS.md)。