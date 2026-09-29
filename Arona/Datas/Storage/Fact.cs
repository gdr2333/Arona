using Microsoft.Data.SqlTypes;
using System.ComponentModel.DataAnnotations;

namespace Arona.Datas.Storage;

public class Fact
{
    [Key]
    public long Id { get; set; }
    public string Content { get; set; }
    public SqlVector<float> Embedding { get; set; }
    public DateTime UtcTime { get; set; }
    public ChatSession? Session { get; set; }
    public string? SessionId { get; set; }
}
