using System.ComponentModel.DataAnnotations;

namespace Arona.Datas.Storage;

public class ChatSession
{
    // botid:group:groupid
    // botid:private:userid
    // webui:sessionid
    [Key]
    public string Id { get; set; }
    public ICollection<ChatMessage> Messages { get; } = [];
    public ICollection<Fact> Facts { get; } = [];
}
