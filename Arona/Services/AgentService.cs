using Arona.Datas;
using Arona.Datas.InMemory;
using Arona.Datas.Storage;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using Microsoft.Extensions.Logging;
using OpenAI;
using System.ClientModel;

namespace Arona.Services;

public class AgentService(MainDbContext dbContext, AIModels models, Config config, ILoggerFactory loggerFactory)
{
    private const string FactRules = """

        ## Fact 工具使用规范
        - 写入 Fact 前必须先调用 SearchFacts 检查是否已存在相同或高度相似的内容，避免重复写入。
        - 如果已存在相似内容，不要重复写入，只需在回复中确认已知即可。
        - 删除 Fact 后应告知用户已删除。

        """;

    private static string BuildOutputRules(ChatScenario scenario) => scenario switch
    {
        ChatScenario.Group => """

            ## 输出规则
            - 你必须通过调用 SendMessageToUser 工具来向用户发送消息，这是你向用户输出消息的主要方式。
            - 你的直接文本输出仅用于内部推理与排障，用户默认看不到；需要让用户看到的内容请通过 SendMessageToUser 工具发送。
            - 当前是群聊场景。你会收到群内所有用户的消息，每条消息格式为「[昵称]: 内容」，昵称即发言者身份。
            - 你有权自主决定是否回复：不要对每条消息都回复。仅在被人直接提及、被 @、或话题与你相关时才回复；遇到与你不相关的场景（闲聊、他人之间的对话、无关话题等）时不要回复。
            - 决定不回复时，不要调用 SendMessageToUser 工具即可。

            """ + FactRules,
        _ => """

            ## 输出规则
            - 你必须通过调用 SendMessageToUser 工具来向用户发送消息，这是你向用户输出消息的主要方式。
            - 你的直接文本输出仅用于内部推理与排障，用户默认看不到；需要让用户看到的内容请通过 SendMessageToUser 工具发送。
            - 你有权决定在某轮不回复用户消息，但当前是单用户对话阶段，你应该回复用户消息。
            - 每次处理完用户请求后，必须通过 SendMessageToUser 工具向用户回复结果。

            """ + FactRules
    };

    public ArAgent GetAgent(string sessionId, ChatScenario scenario = ChatScenario.WebUI)
    {
        var logger = loggerFactory.CreateLogger<ArAgent>();
        logger.LogDebug("为会话 {Session} 创建 Agent（场景 {Scenario}）", sessionId, scenario);

        if (models.ChatModel is null)
        {
            logger.LogError("未配置聊天模型，无法为会话 {Session} 创建 Agent", sessionId);
            throw new InvalidOperationException("未配置聊天模型。");
        }

        var client = new OpenAIClient(new ApiKeyCredential(models.ChatModel.ApiKey), new()
        {
            Endpoint = models.ChatModel.Endpoint,
        });
        var chatClient = client.GetChatClient(models.ChatModel.ModelId);
        var aiChatClient = chatClient.AsIChatClient();

        var basePrompt = dbContext.Configs.Find("Prompt")?.Value
            ?? "请告诉你的所有者，你没有配置提示词。快去吧。";
        var instructions = basePrompt + BuildOutputRules(scenario);

        var tools = new List<AITool> { AgentTools.CreateSendMessageTool() };
        var factTools = new FactTools(dbContext, models, config, sessionId, loggerFactory.CreateLogger<FactTools>());
        tools.AddRange(factTools.CreateTools());

        var innerAgent = aiChatClient.AsAIAgent(
            instructions,
            null,
            null,
            tools
        );

        logger.LogInformation("会话 {Session}：使用模型 {Model} @ {Endpoint} 创建 Agent",
            sessionId, models.ChatModel.ModelId, models.ChatModel.Endpoint);

        return new ArAgent(innerAgent, dbContext, sessionId, models, config, logger);
    }
}
