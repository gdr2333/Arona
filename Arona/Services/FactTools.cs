using Arona.Datas;
using Arona.Datas.InMemory;
using Arona.Datas.Storage;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Embeddings;
using System.ClientModel;
using System.Text.Json;

namespace Arona.Services;

public class FactTools
{
    private readonly MainDbContext _dbContext;
    private readonly AIModels _models;
    private readonly Config _config;
    private readonly string _sessionId;
    private readonly ILogger<FactTools> _logger;

    public FactTools(MainDbContext dbContext, AIModels models, Config config, string sessionId, ILogger<FactTools> logger)
    {
        _dbContext = dbContext;
        _models = models;
        _config = config;
        _sessionId = sessionId;
        _logger = logger;
    }

    public List<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(WriteFact,
            name: "WriteFact",
            description: "写入一条 Fact（事实/记忆）。写入前务必先调用 SearchFacts 检查是否已存在相同或高度相似的内容，避免重复写入。scope=\"global\" 为全局 Fact，所有会话可见；scope=\"session\" 为当前会话专属 Fact。"),
        AIFunctionFactory.Create(SearchFacts,
            name: "SearchFacts",
            description: "按语义相似度（向量）检索 Fact。scope 可选 \"global\"、\"session\"、\"all\"，默认 \"all\"。返回最相关的若干条。"),
        AIFunctionFactory.Create(SearchFactsByKeywords,
            name: "SearchFactsByKeywords",
            description: "按关键词（SQL Server 全文检索）查找 Fact。scope 同上。适合用具体词语快速查找。"),
        AIFunctionFactory.Create(DeleteFact,
            name: "DeleteFact",
            description: "按 ID 删除一条 Fact。"),
        AIFunctionFactory.Create(SearchHistory,
            name: "SearchHistory",
            description: "在当前会话的历史消息中，按语义相似度+全文检索与 content 相关的记录。返回相关的历史消息。"),
    ];

    private async Task<string> WriteFact(string content, string scope, CancellationToken ct)
    {
        var sessionId = scope == "session" ? _sessionId : null;
        var embedding = await ComputeEmbeddingAsync(content, ct);
        var fact = new Fact
        {
            Content = content,
            Embedding = embedding,
            UtcTime = DateTime.UtcNow,
            SessionId = sessionId
        };
        _dbContext.Facts.Add(fact);
        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("写入 Fact：scope={Scope}, id={Id}, session={Session}", scope, fact.Id, _sessionId);
        return $"已写入 Fact（ID={fact.Id}, scope={scope}）。";
    }

    private async Task<string> SearchFacts(string content, string scope, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) limit = 10;
        var queryVec = await ComputeEmbeddingAsync(content, ct);
        var query = ScopeFilter(_dbContext.Facts, scope);
        var results = await query
            .OrderBy(f => EF.Functions.VectorDistance("cosine", f.Embedding, queryVec))
            .Take(limit)
            .Select(f => new { f.Id, f.Content })
            .ToArrayAsync(ct);
        _logger.LogInformation("向量检索 Fact：scope={Scope}, 结果={Count}", scope, results.Length);
        return FormatResults("Fact", results.Select(r => (r.Id, r.Content)).ToArray());
    }

    private async Task<string> SearchFactsByKeywords(string keywords, string scope, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) limit = 10;
        var condition = BuildContains(keywords);
        var query = ScopeFilter(_dbContext.Facts, scope);
        var results = await query
            .Where(f => EF.Functions.Contains(f.Content, condition))
            .OrderByDescending(f => f.UtcTime)
            .Take(limit)
            .Select(f => new { f.Id, f.Content })
            .ToArrayAsync(ct);
        _logger.LogInformation("全文检索 Fact：keywords={Keywords}, scope={Scope}, 结果={Count}", keywords, scope, results.Length);
        return FormatResults("Fact", results.Select(r => (r.Id, r.Content)).ToArray());
    }

    private async Task<string> DeleteFact(long id, CancellationToken ct)
    {
        var fact = await _dbContext.Facts.FindAsync([id], ct);
        if (fact is null)
        {
            _logger.LogWarning("删除 Fact 失败：id={Id} 不存在", id);
            return $"未找到 Fact（ID={id}）。";
        }
        _dbContext.Facts.Remove(fact);
        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("删除 Fact：id={Id}", id);
        return $"已删除 Fact（ID={id}）。";
    }

    private async Task<string> SearchHistory(string content, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) limit = 10;
        var queryVec = await ComputeEmbeddingAsync(content, ct);

        var vecResults = await _dbContext.ChatMessages
            .Where(m => m.SessionId == _sessionId)
            .OrderBy(m => EF.Functions.VectorDistance("cosine", m.Embedding, queryVec))
            .Take(limit)
            .Select(m => new { m.Role, m.Message, m.UtcTime })
            .ToArrayAsync(ct);

        var ftResults = await _dbContext.ChatMessages
            .Where(m => m.SessionId == _sessionId && EF.Functions.Contains(m.Message, BuildContains(content)))
            .OrderByDescending(m => m.UtcTime)
            .Take(limit)
            .Select(m => new { m.Role, m.Message, m.UtcTime })
            .ToArrayAsync(ct);

        var merged = vecResults
            .Concat(ftResults)
            .DistinctBy(m => m.Message)
            .Take(limit * 2)
            .Select(m => $"[{m.Role}] {m.Message}")
            .ToArray();

        _logger.LogInformation("检索历史：session={Session}, 向量={Vec}, 全文={Ft}, 合并={Merged}",
            _sessionId, vecResults.Length, ftResults.Length, merged.Length);
        return merged.Length == 0 ? "未找到相关历史记录。" : $"找到 {merged.Length} 条相关历史:\n" + string.Join("\n", merged);
    }

    private IQueryable<Fact> ScopeFilter(IQueryable<Fact> query, string scope) => scope switch
    {
        "global" => query.Where(f => f.SessionId == null),
        "session" => query.Where(f => f.SessionId == _sessionId),
        _ => query.Where(f => f.SessionId == null || f.SessionId == _sessionId)
    };

    private static string BuildContains(string keywords)
    {
        var words = keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => keywords,
            1 => words[0],
            _ => string.Join(" OR ", words)
        };
    }

    private static string FormatResults(string label, (long Id, string Content)[] results)
    {
        if (results.Length == 0) return $"未找到{label}。";
        var lines = results.Select(r => $"[ID:{r.Id}] {r.Content}");
        return $"找到 {results.Length} 条{label}:\n" + string.Join("\n", lines);
    }

    private async Task<SqlVector<float>> ComputeEmbeddingAsync(string text, CancellationToken ct)
    {
        var embModel = _models.EmbeddingModel;
        if (embModel is null || string.IsNullOrWhiteSpace(text))
            return new SqlVector<float>(new float[_config.EmbeddingDimension]);
        try
        {
            var client = new OpenAIClient(new ApiKeyCredential(embModel.ApiKey), new()
            {
                Endpoint = embModel.Endpoint,
            });
            var embeddingClient = client.GetEmbeddingClient(embModel.ModelId);
            var result = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct);
            return new SqlVector<float>(result.Value.ToFloats());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "计算嵌入失败，回退为零向量");
            return new SqlVector<float>(new float[_config.EmbeddingDimension]);
        }
    }
}