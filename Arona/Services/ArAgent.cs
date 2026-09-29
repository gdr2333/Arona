using Arona.Datas;
using Arona.Datas.InMemory;
using Arona.Datas.Storage;
using Microsoft.Agents.AI;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Embeddings;
using System.ClientModel;
using DbChatMessage = Arona.Datas.Storage.ChatMessage;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Arona.Services;

public class ArAgent : DelegatingAIAgent
{
    private readonly MainDbContext _dbContext;
    private readonly string _sessionId;
    private readonly AIModels _models;
    private readonly Config _config;
    private readonly ILogger<ArAgent> _logger;
    private const int DefaultContextLength = 20;

    public ArAgent(AIAgent innerAgent, MainDbContext dbContext, string sessionId,
        AIModels models, Config config, ILogger<ArAgent> logger)
        : base(innerAgent)
    {
        _dbContext = dbContext;
        _sessionId = sessionId;
        _models = models;
        _config = config;
        _logger = logger;
    }

    private static ChatRole ToChatRole(string role) => role switch
    {
        "user" => ChatRole.User,
        "assistant" => ChatRole.Assistant,
        "system" => ChatRole.System,
        "tool" => ChatRole.Tool,
        _ => new ChatRole(role)
    };

    private int ResolveContextLength()
    {
        var raw = _dbContext.Configs.Find("ContextLength")?.Value;
        if (raw is null)
        {
            _logger.LogWarning("Configs 中未找到 ContextLength，使用默认值 {Default}", DefaultContextLength);
            return DefaultContextLength;
        }
        if (!int.TryParse(raw, out var cl) || cl <= 0)
        {
            _logger.LogWarning("ContextLength 配置值 \"{Raw}\" 无效，使用默认值 {Default}", raw, DefaultContextLength);
            return DefaultContextLength;
        }
        return cl;
    }

    private async Task<SqlVector<float>> ComputeEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var embModel = _models.EmbeddingModel;
        if (embModel is null || string.IsNullOrWhiteSpace(text))
            return CreateZeroVector();

        try
        {
            var client = new OpenAIClient(new ApiKeyCredential(embModel.ApiKey), new()
            {
                Endpoint = embModel.Endpoint,
            });
            var embeddingClient = client.GetEmbeddingClient(embModel.ModelId);
            var result = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
            return new SqlVector<float>(result.Value.ToFloats());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "计算嵌入失败，回退为零向量");
            return CreateZeroVector();
        }
    }

    private SqlVector<float> CreateZeroVector() => new(new float[_config.EmbeddingDimension]);

    private DbChatMessage CreateMessage(string role, string text, DateTime now, SqlVector<float> embedding) => new()
    {
        SessionId = _sessionId,
        Role = role,
        Message = text,
        Embedding = embedding,
        UtcTime = now
    };

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<AiChatMessage> messages,
        AgentSession session,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        var messagesList = messages.ToList();
        var contextLength = ResolveContextLength();
        _logger.LogDebug("会话 {Session}：滑动窗口大小 {Window}，本轮输入 {Count} 条消息",
            _sessionId, contextLength, messagesList.Count);

        var history = await _dbContext.ChatMessages
            .Where(m => m.SessionId == _sessionId)
            .OrderByDescending(m => m.UtcTime)
            .Take(contextLength)
            .OrderBy(m => m.UtcTime)
            .Select(m => new AiChatMessage(ToChatRole(m.Role), m.Message))
            .ToListAsync(cancellationToken);
        _logger.LogDebug("会话 {Session}：从数据库加载 {Count} 条历史消息", _sessionId, history.Count);

        await EnsureSessionExistsAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var userSaved = 0;
        foreach (var msg in messagesList)
        {
            if (msg.Role == ChatRole.User && !string.IsNullOrWhiteSpace(msg.Text))
            {
                var embedding = await ComputeEmbeddingAsync(msg.Text, cancellationToken);
                _dbContext.ChatMessages.Add(CreateMessage("user", msg.Text, now, embedding));
                userSaved++;
            }
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (userSaved > 0)
            _logger.LogInformation("会话 {Session}：保存 {Count} 条用户消息", _sessionId, userSaved);

        var combined = history.Concat(messagesList);
        _logger.LogInformation("会话 {Session}：调用内部 Agent（共 {Total} 条消息）", _sessionId, history.Count + messagesList.Count);
        var response = await InnerAgent.RunAsync(combined, null, options, cancellationToken);
        _logger.LogInformation("会话 {Session}：Agent 返回 {Count} 条响应消息，FinishReason={Reason}",
            _sessionId, response.Messages.Count, response.FinishReason);

        now = DateTime.UtcNow;
        var assistantSaved = 0;
        foreach (var msg in response.Messages)
        {
            foreach (var content in msg.Contents)
            {
                if (content is FunctionCallContent fc)
                {
                    var args = fc.Arguments is not null
                        ? string.Join(", ", fc.Arguments.Select(kv => $"{kv.Key}={kv.Value}"))
                        : "(无)";
                    _logger.LogInformation("会话 {Session}：工具调用 {Tool}，参数：{Args}", _sessionId, fc.Name, args);

                    if (fc.Name == AgentTools.SendMessageToolName
                        && fc.Arguments is not null
                        && fc.Arguments.TryGetValue("message", out var m))
                    {
                        var sent = AgentTools.ExtractArgumentValue(m);
                        if (!string.IsNullOrWhiteSpace(sent))
                        {
                            var embedding = await ComputeEmbeddingAsync(sent!, cancellationToken);
                            _dbContext.ChatMessages.Add(CreateMessage("assistant", sent!, now, embedding));
                            assistantSaved++;
                        }
                    }
                }
                else if (content is FunctionResultContent frc)
                {
                    _logger.LogInformation("会话 {Session}：工具结果 callId={CallId}：{Result}", _sessionId, frc.CallId, frc.Result);
                }
                else if (content is TextContent tc && !string.IsNullOrWhiteSpace(tc.Text))
                {
                    _logger.LogInformation("会话 {Session} 排障/推理：{Text}", _sessionId, tc.Text);
                }
            }
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (assistantSaved > 0)
            _logger.LogDebug("会话 {Session}：保存 {Count} 条 Agent 响应消息", _sessionId, assistantSaved);

        return response;
    }

    private async Task EnsureSessionExistsAsync(CancellationToken cancellationToken)
    {
        if (!await _dbContext.ChatSessions.AnyAsync(s => s.Id == _sessionId, cancellationToken))
        {
            _dbContext.ChatSessions.Add(new ChatSession { Id = _sessionId });
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("会话 {Session}：创建新的 ChatSession 记录", _sessionId);
        }
    }
}