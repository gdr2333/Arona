using Microsoft.Extensions.AI;

namespace Arona.Services;

public static class AgentTools
{
    public const string SendMessageToolName = "SendMessageToUser";

    public static AIFunction CreateSendMessageTool() =>
        AIFunctionFactory.Create(
            SendMessageImpl,
            name: SendMessageToolName,
            description: "向用户发送消息。通过此工具输出你要对用户说的内容，这是你向用户输出消息的主要方式。");

    private static string SendMessageImpl(string message) => "已发送";

    public static string? ExtractArgumentValue(object? value)
    {
        if (value is string s) return s;
        if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.String)
            return je.GetString();
        return value?.ToString();
    }
}