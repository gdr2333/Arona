using System.ComponentModel.DataAnnotations;

namespace Arona.Datas.Storage;

public class ApiProvider
{
    [Key]
    public string Id { get; set; }
    public string Name { get; set; }
    public string ApiKey { get; set; }
    public ICollection<ChatModel> ChatModels { get; set; } = [];
    public ICollection<EmbeddingModel> EmbeddingModels { get; set; } = [];
}
