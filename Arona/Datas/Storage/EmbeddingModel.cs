using Microsoft.EntityFrameworkCore;

namespace Arona.Datas.Storage;

[PrimaryKey(nameof(ModelId), nameof(ProviderId))]
public class EmbeddingModel
{
    public required string ModelId { get; set; }
    public ApiProvider? Provider { get; set; }
    public required string ProviderId { get; set; }
    public required string ModelName { get; set; }
    public required Uri ModelEndpoint { get; set; }
    public int Dimensions { get; set; }
    public bool IsFixedDimension { get; set; } = true;
}
