using Microsoft.EntityFrameworkCore;

namespace Arona.Datas.Storage;

[PrimaryKey(nameof(ModelId), nameof(ProviderId))]
public class ChatModel
{
    public string ModelId { get; set; }
    public string ProviderId { get; set; }
    public ApiProvider Provider { get; set; }
    public string ModelName { get; set; }
    public Uri ModelEndpoint { get; set; }
}
