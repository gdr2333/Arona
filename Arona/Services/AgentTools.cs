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

    private static string SendMessageImpl(string message) =>
        "消息已成功发送给用户，本轮输出已完成，请勿再次调用此工具发送相同内容。";

    public static string? ExtractArgumentValue(object? value)
    {
        if (value is string s) return s;
        if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.String)
            return je.GetString();
        return value?.ToString();
    }

    public static List<string> ExtractSendMessageContents(IEnumerable<ChatMessage> messages)
    {
        var result = new List<string>();
        string? last = null;
        foreach (var msg in messages)
        {
            foreach (var content in msg.Contents)
            {
                if (content is FunctionCallContent fc
                    && fc.Name == SendMessageToolName
                    && fc.Arguments is not null
                    && fc.Arguments.TryGetValue("message", out var m))
                {
                    var sent = ExtractArgumentValue(m);
                    if (string.IsNullOrWhiteSpace(sent)) continue;
                    if (last is not null && string.Equals(last, sent, StringComparison.Ordinal)) continue;
                    result.Add(sent!);
                    last = sent;
                }
            }
        }
        return result;
    }
}