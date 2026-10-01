using Arona.Components;
using Arona.Datas;
using Arona.Datas.InMemory;
using Arona.Datas.Storage;
using Arona.Services;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration.GetRequiredSection("AronaConfig").Get<Config>() ?? throw new NullReferenceException("配置文件读取失败！");

builder.Services.AddSingleton(config);

builder.Services.AddSqlServer<MainDbContext>(config.MainDb);

builder.Services.AddScoped<AIModels>();

builder.Services.AddScoped<AgentService>();

builder.Services.AddScoped<EmbeddingService>();

builder.Services.AddSingleton<GroupBlacklistService>();
builder.Services.AddHostedService<OnebotService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();



var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    using var sqlsvr = scope.ServiceProvider.GetRequiredService<MainDbContext>();
    if (args.Contains("--cleardb"))
        sqlsvr.Database.EnsureDeleted();
    sqlsvr.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
