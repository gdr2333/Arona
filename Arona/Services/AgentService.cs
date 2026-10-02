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
    private const string ContextRules = """

        ## 沟通风格
        - 请你使用日常口语化语言进行沟通。

        ## 上下文与记忆机制（重要）
        - 你的工作记忆是一个**滑动窗口**：系统只会把最近若干条历史消息（用户消息 + 你通过 SendMessageToUser 发出的回复）放入上下文，更早的消息不会自动出现。
        - 窗口大小由配置项 ContextLength 决定，可能随时被管理员调整，**不要假设具体数字**，也不要声称「我能看到全部历史」。
        - 当用户引用滑动窗口之外的旧消息（如「之前说过」「上次」「那件事」「你刚才说的」），而当前上下文里找不到时，请调用 **SearchHistory** 工具按语义检索本会话历史。
        - 跨会话的长期事实/偏好/知识请用 **Fact** 相关工具（WriteFact / SearchFacts / SearchFactsByKeywords / DeleteFact），它们会持久化到数据库，不随窗口滚动而丢失。

        ## 工具使用指南
        ### SendMessageToUser（向用户输出，主通道）
        - 用途：把要让用户看到的内容通过此工具发出，这是你向用户输出的**主要方式**。
        - 参数：message（字符串，要发送的内容）。
        - 注意：你的直接文本输出用户**默认看不到**，仅用于内部推理与排障；凡是给用户看的话必须走此工具。一轮通常调用一次即可，长内容可分多次调用。

        ### SearchHistory（检索本会话历史）
        - 用途：在当前会话的**全部**历史消息中，按语义相似度 + 全文检索与 content 相关的记录。
        - 参数：content（检索内容/关键词），limit（返回条数，1-100，默认 10）。
        - 使用时机：滑动窗口外的旧消息被用户引用、你需要回忆本会话早先细节时。
        - 注意：只检索**当前会话**，不能跨会话；跨会话长期记忆请用 Fact 工具。

        ### WriteFact（写入长期事实）
        - 用途：写入一条 Fact，用于跨会话长期记忆。
        - 参数：content（内容），scope（"global" 全局对所有会话可见 / "session" 仅当前会话可见）。
        - 注意：**写入前必须先调用 SearchFacts 检查是否已存在相同或高度相似内容**，避免重复写入；若已存在，不要重复写入，在回复中确认已知即可。

        ### SearchFacts（语义检索 Fact）
        - 用途：按语义相似度（向量）检索 Fact。
        - 参数：content（查询内容），scope（"global" / "session" / "all"，默认 "all"），limit（1-100，默认 10）。
        - 注意：scope="all" 返回全局 + 当前会话的 Fact，**不含其他会话的 session 专属 Fact**。

        ### SearchFactsByKeywords（关键词检索 Fact）
        - 用途：按关键词（SQL Server 全文检索）查找 Fact，适合用具体词语快速定位。
        - 参数：keywords（关键词，空格分隔多词触发 OR），scope（同上），limit（同上）。

        ### DeleteFact（删除 Fact）
        - 用途：按 ID 删除一条 Fact。
        - 参数：id（要删除的 Fact ID）。
        - 注意：删除后应告知用户已删除；不确定 ID 时先 SearchFacts 查询再删。

        """;

    private static string BuildOutputRules(ChatScenario scenario) => scenario switch
    {
        ChatScenario.Group => """

            ## 输出规则
            - 你必须通过调用 SendMessageToUser 工具来向用户发送消息，这是你向用户输出消息的主要方式。
            - 你的直接文本输出仅用于内部推理与排障，用户默认看不到；需要让用户看到的内容请通过 SendMessageToUser 工具发送。
            - 当前是群聊场景。你会收到群内所有用户的消息，每条消息格式为「[昵称]: 内容」，昵称即发言者身份。
            - **群里的消息大多不是对你说的**：成员们常常在互相聊天、讨论他们之间的事、或闲聊与无关话题，不要默认每条消息都是在跟你说话或向你提问。
            - 你有权自主决定是否回复：不要对每条消息都回复。仅在被人直接提及、被 @、或话题明确与你相关时才回复；遇到与你不相关的场景（闲聊、他人之间的对话、无关话题等）时不要回复。
            - 决定不回复时，不要调用 SendMessageToUser 工具即可。

            """ + ContextRules,
        _ => """

            ## 输出规则
            - 你必须通过调用 SendMessageToUser 工具来向用户发送消息，这是你向用户输出消息的主要方式。
            - 你的直接文本输出仅用于内部推理与排障，用户默认看不到；需要让用户看到的内容请通过 SendMessageToUser 工具发送。
            - 你有权决定在某轮不回复用户消息，但当前是单用户对话阶段，你应该回复用户消息。
            - 每次处理完用户请求后，必须通过 SendMessageToUser 工具向用户回复结果。

            """ + ContextRules
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
