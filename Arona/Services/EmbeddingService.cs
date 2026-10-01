using Arona.Datas;
using Arona.Datas.InMemory;
using Microsoft.Data.SqlTypes;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Embeddings;
using System.ClientModel;

namespace Arona.Services;

public class EmbeddingService
{
    private readonly AIModels _models;
    private readonly Config _config;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(AIModels models, Config config, ILogger<EmbeddingService> logger)
    {
        _models = models;
        _config = config;
        _logger = logger;
    }

    public async Task<SqlVector<float>> ComputeAsync(string text, CancellationToken ct = default)
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