using Microsoft.Data.SqlTypes;
using System.ComponentModel.DataAnnotations;

namespace Arona.Datas.Storage;

public class ChatMessage
{
    [Key]
    public long Id { get; set; }
    public required string SessionId { get; set; }
    public required string Role { get; set; }
    public ChatSession? Session { get; set; }
    public required string Message { get; set; }
    public SqlVector<float> Embedding { get; set; }
    public DateTime UtcTime { get; set; }
}
