using Microsoft.EntityFrameworkCore;

namespace Arona.Datas.Storage;

[PrimaryKey(nameof(ModelId), nameof(ProviderId))]
public class EmbeddingModel
{
    public string ModelId { get; set; }
    public ApiProvider Provider { get; set; }
    public string ProviderId { get; set; }
    public string ModelName { get; set; }
    public Uri ModelEndpoint { get; set; }
    public int Dimensions { get; set; }
    public bool IsFixedDimension { get; set; } = true;
}
