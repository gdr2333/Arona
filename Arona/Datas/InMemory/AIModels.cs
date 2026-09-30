using Arona.Datas.Storage;

namespace Arona.Datas.InMemory;

public class AIModels
{
    int embeddingDimensions;
    public AIModels(MainDbContext dbContext, Config config)
    {
        embeddingDimensions = config.EmbeddingDimension;
        Reload(dbContext);
    }

    public void Reload(MainDbContext dbContext)
    {
        var chatModel = dbContext.Configs.Find("ChatModel");
        var embeddingModel = dbContext.Configs.Find("EmbeddingModel");
        if (chatModel is null)
            ChatModel = null;
        else
        {
            var chatModelSplited = chatModel.Value.Split(":");
            var chatModelProv = dbContext.ApiProviders.Find(chatModelSplited[0])
                ?? throw new InvalidOperationException($"未找到 ApiProvider: {chatModelSplited[0]}");
            dbContext.Entry(chatModelProv)
                .Collection(cmp => cmp.ChatModels)
                .Load();
            var chatModelObj = chatModelProv.ChatModels.FirstOrDefault(cm => cm.ModelName == chatModelSplited[1])
                ?? throw new InvalidOperationException($"未找到 ChatModel: {chatModelSplited[1]}");
            ChatModel = new()
            {
                Id = chatModel.Value,
                ApiKey = chatModelProv.ApiKey,
                ModelId = chatModelObj.ModelId,
                Endpoint = chatModelObj.ModelEndpoint,
                Name = chatModelObj.ModelName
            };
        }
        if (embeddingModel is null)
            EmbeddingModel = null;
        else
        {
            var embeddingModelSplited = embeddingModel.Value.Split(":");
            var embeddingModelProv = dbContext.ApiProviders.Find(embeddingModelSplited[0])
                ?? throw new InvalidOperationException($"未找到 ApiProvider: {embeddingModelSplited[0]}");
            dbContext.Entry(embeddingModelProv)
                .Collection(cmp => cmp.EmbeddingModels)
                .Load();
            var embeddingModelObj = embeddingModelProv.EmbeddingModels.FirstOrDefault(cm => cm.ModelName == embeddingModelSplited[1])
                ?? throw new InvalidOperationException($"未找到 EmbeddingModel: {embeddingModelSplited[1]}");
            if (embeddingModelObj.Dimensions != embeddingDimensions)
                throw new InvalidOperationException("模型的向量维度与配置的向量维度不匹配！");
            EmbeddingModel = new()
            {
                Id = embeddingModel.Value,
                ApiKey = embeddingModelProv.ApiKey,
                ModelId = embeddingModelObj.ModelId,
                Endpoint = embeddingModelObj.ModelEndpoint,
                Name = embeddingModelObj.ModelName,
                Dimensions = embeddingModelObj.Dimensions,
                IsFixedDimension = embeddingModelObj.IsFixedDimension
            };
        }
    }

    public void SaveToDb(MainDbContext dbContext)
    {
        var chatModel = dbContext.Configs.Find("ChatModel");
        if (ChatModel is null && chatModel is not null)
            dbContext.Configs.Remove(chatModel);
        else if (ChatModel is not null)
        {
            if (chatModel is not null)
                chatModel.Value = ChatModel.Id;
            else
                dbContext.Configs.Add(new() { Id = "ChatModel", Value = ChatModel.Id });
        }
        var embeddingModel = dbContext.Configs.Find("EmbeddingModel");
        if (EmbeddingModel is null && embeddingModel is not null)
            dbContext.Configs.Remove(embeddingModel);
        else if (EmbeddingModel is not null)
        {
            if (embeddingModel is not null)
                embeddingModel.Value = EmbeddingModel.Id;
            else
                dbContext.Configs.Add(new() { Id = "EmbeddingModel", Value = EmbeddingModel.Id });
        }
        dbContext.SaveChanges();
    }

    public MemoryChatModel? ChatModel { get; set; }
    public MemoryEmbeddingModel? EmbeddingModel { get; set; }
}
