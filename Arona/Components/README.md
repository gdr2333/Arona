# Components

Blazor 组件根目录。Interactive Server 渲染模式。

## 文件

- `App.razor` — 应用根（HTML 文档骨架，引入 FluentUI 样式、Blazor 脚本）。
- `Routes.razor` — 路由器，默认布局 `Layout.MainLayout`，未匹配走 `Pages.NotFound`。
- `_Imports.razor` — 全局 using 与组件命名空间导入。

## 子目录

- [`Layout/`](Layout/README.md) — 布局组件
- [`Pages/`](Pages/README.md) — 路由页面