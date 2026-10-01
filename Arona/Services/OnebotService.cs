using System.Collections.Concurrent;
using System.Text;
using Arona.Datas;
using Gdr2333.BotLib.OnebotV11.Clients;
using Gdr2333.BotLib.OnebotV11.Events;
using Gdr2333.BotLib.OnebotV11.Messages;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Arona.Services;

public sealed class OnebotService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly Config _config;
    private readonly GroupBlacklistService _blacklist;
    private readonly ILogger<OnebotService> _logger;
    private ReverseWebSocketClient? _client;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sessionLocks = new();
    private readonly ConcurrentDictionary<long, ConcurrentQueue<string>> _groupBuffers = new();
    private readonly ConcurrentDictionary<long, long> _groupBotMap = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private static readonly TimeSpan GroupFlushInterval = TimeSpan.FromSeconds(30);

    public OnebotService(IServiceProvider services, Config config, GroupBlacklistService blacklist, ILogger<OnebotService> logger)
    {
        _services = services;
        _config = config;
        _blacklist = blacklist;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var uri = _config.OnebotListenUri;
        if (string.IsNullOrWhiteSpace(uri))
        {
            _logger.LogInformation("未配置 OnebotListenUri，OnebotV11 反向 WebSocket 不启用");
            return Task.CompletedTask;
        }

        _blacklist.Load();

        _client = new ReverseWebSocketClient(new Uri(uri), _config.OnebotAccessToken ?? string.Empty);
        _client.OnEventOccurrence += OnEvent;
        _client.OnExceptionOccurrence += OnException;
        _client.Start();
        _logger.LogInformation("OnebotV11 反向 WebSocket 已启动，监听 {Uri}", uri);

        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => GroupFlushLoopAsync(_loopCts.Token));
        _logger.LogInformation("群消息批量刷新循环已启动，间隔 {Interval}", GroupFlushInterval);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_loopCts is not null)
        {
            _loopCts.Cancel();
            if (_loopTask is not null)
            {
                try { await _loopTask; }
                catch (Exception ex) { _logger.LogWarning(ex, "群消息刷新循环停止时出错"); }
            }
            _loopCts.Dispose();
            _loopCts = null;
        }

        if (_client is not null)
        {
            _client.OnEventOccurrence -= OnEvent;
            _client.OnExceptionOccurrence -= OnException;
            try { _client.Stop(); }
            catch (Exception ex) { _logger.LogWarning(ex, "停止 OnebotV11 客户端时出错"); }
            _client.Dispose();
            _client = null;
            _logger.LogInformation("OnebotV11 反向 WebSocket 已停止");
        }
    }

    private void OnException(object? sender, Exception e)
    {
        _logger.LogWarning(e, "OnebotV11 连接异常");
    }

    private async void OnEvent(object? sender, OnebotV11EventArgsBase e)
    {
        try
        {
            switch (e)
            {
                case PrivateMessageReceivedEventArgs pm:
                    await HandlePrivateAsync(pm);
                    break;
                case GroupMessageReceivedEventArgs gm:
                    await HandleGroupAsync(gm);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 Onebot 事件失败");
        }
    }

    private async Task HandlePrivateAsync(PrivateMessageReceivedEventArgs e)
    {
        var text = ExtractText(e.Message);
        if (string.IsNullOrWhiteSpace(text)) return;

        var sessionId = $"{e.BotId}:private:{e.UserId}";
        var nickname = e.Sender?.Nickname ?? e.UserId.ToString();
        var prompt = $"[{nickname}]: {text}";
        _logger.LogInformation("Onebot 私聊消息：bot={Bot}, user={User}({Nick}), session={Session}", e.BotId, e.UserId, nickname, sessionId);

        var reply = await RunAgentAsync(sessionId, prompt, ChatScenario.Private);
        if (string.IsNullOrEmpty(reply) || _client is null) return;

        await _client.SendPrivateMessageAsync(e.UserId, new Message(reply));
    }

    private Task HandleGroupAsync(GroupMessageReceivedEventArgs e)
    {
        if (_blacklist.IsBlacklisted(e.GroupId))
        {
            _logger.LogDebug("群 {Group} 在黑名单中，忽略消息", e.GroupId);
            return Task.CompletedTask;
        }

        var text = ExtractText(e.Message);
        if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;

        var nickname = !string.IsNullOrWhiteSpace(e.GroupSender?.CardInfo)
            ? e.GroupSender!.CardInfo
            : e.GroupSender?.Nickname ?? e.UserId.ToString();
        var prompt = $"[{nickname}]: {text}";

        var queue = _groupBuffers.GetOrAdd(e.GroupId, _ => new ConcurrentQueue<string>());
        queue.Enqueue(prompt);
        _groupBotMap[e.GroupId] = e.BotId;
        _logger.LogDebug("群 {Group} 缓冲消息：bot={Bot}, user={User}({Nick})", e.GroupId, e.BotId, e.UserId, nickname);
        return Task.CompletedTask;
    }

    private async Task GroupFlushLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(GroupFlushInterval, ct);
                await FlushAllGroupsAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "群消息刷新循环异常");
            }
        }
    }

    private async Task FlushAllGroupsAsync(CancellationToken ct)
    {
        var pending = new List<(long botId, long groupId, List<string> messages)>();
        foreach (var (groupId, queue) in _groupBuffers)
        {
            if (queue.IsEmpty) continue;
            var messages = new List<string>();
            while (queue.TryDequeue(out var msg))
                messages.Add(msg);
            if (messages.Count == 0) continue;
            if (!_groupBotMap.TryGetValue(groupId, out var botId)) continue;
            pending.Add((botId, groupId, messages));
        }
        if (pending.Count == 0) return;

        _logger.LogInformation("群消息批量刷新：{Count} 个群有待处理消息", pending.Count);
        await Task.WhenAll(pending.Select(p => FlushGroupAsync(p.botId, p.groupId, p.messages, ct)));
    }

    private async Task FlushGroupAsync(long botId, long groupId, List<string> messages, CancellationToken ct)
    {
        var sessionId = $"{botId}:group:{groupId}";
        var combined = string.Join('\n', messages);
        _logger.LogInformation("群 {Group} 批量注入 {Count} 条消息，session={Session}", groupId, messages.Count, sessionId);
        try
        {
            var reply = await RunAgentAsync(sessionId, combined, ChatScenario.Group);
            if (string.IsNullOrEmpty(reply) || _client is null) return;
            await _client.SendGroupMessageAsync(groupId, new Message(reply), ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "群 {Group} 批量处理失败", groupId);
        }
    }

    private async Task<string?> RunAgentAsync(string sessionId, string text, ChatScenario scenario)
    {
        var sem = _sessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            using var scope = _services.CreateScope();
            var agentService = scope.ServiceProvider.GetRequiredService<AgentService>();
            var agent = agentService.GetAgent(sessionId, scenario);
            var response = await agent.RunAsync(text);

            var contents = AgentTools.ExtractSendMessageContents(response.Messages);
            return contents.Count == 0 ? null : string.Join('\n', contents);
        }
        finally
        {
            sem.Release();
        }
    }

    private static string ExtractText(Message message)
    {
        var sb = new StringBuilder();
        foreach (var part in message)
        {
            if (part is TextPart tp)
                sb.Append(tp.Text);
        }
        return sb.ToString().Trim();
    }

}