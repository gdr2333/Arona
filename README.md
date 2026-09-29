# 开发代号A.R.O.N.A.

（虽然名字是阿罗娜但实际上是个通用AI Bot

启发自[MaiBot](https://github.com/Mai-with-u/MaiBot)项目，目标是一个简单但功能相对全面的AI机器人。

本项目使用C#/.NET+EF Core+SQL Server构建，~~作者认为这会很好玩~~

## 功能

- 自动滑动窗口
- 主动发言
- 长期记忆

## 部署

目前没有Release文件，请从源码Build后部署。

### 源码构建

你需要：

- .NET 10 SDK
- SQL Server 2025 (LocalDB/Express均可)
- 最好是Windows环境，当然你能在Linux上给这俩东西弄明白我也没话说。

首先，构建Arona项目

```sh
dotnet publish Arona/Arona.csproj
```

然后，配置appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AronaConfig": {
    "MainDb": "你的SQL Server ADO.NET连接字符串",
    "AdminPassword": "管理页面密码",
    "EmbeddingDimension": 向量维度,
    "OnebotListenUri": "反向WS监听地址，注意是http://开头",
    "OnebotAccessToken": "反向WS认证Token（可选）"
  },
  "AllowedHosts": "*"
}
```

然后启动，在WebUI配置中配置你的AI提供商与模型，就可以正常对话了。

## 开发与贡献

开发环境与源码构建环境相同。