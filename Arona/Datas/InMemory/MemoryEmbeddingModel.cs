namespace Arona.Datas.InMemory;

public class MemoryEmbeddingModel
{
    public string Id { get; set; }
    public string ModelId { get; set; }
    public string Name { get; set; }
    public Uri Endpoint { get; set; }
    public string ApiKey { get; set; }
    public int Dimensions { get; set; }
    public bool IsFixedDimension { get; set; } = true;
}
