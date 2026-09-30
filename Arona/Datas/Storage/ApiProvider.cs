using System.ComponentModel.DataAnnotations;

namespace Arona.Datas.Storage;

public class ApiProvider
{
    [Key]
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string ApiKey { get; set; }
    public ICollection<ChatModel> ChatModels { get; set; } = [];
    public ICollection<EmbeddingModel> EmbeddingModels { get; set; } = [];
}
