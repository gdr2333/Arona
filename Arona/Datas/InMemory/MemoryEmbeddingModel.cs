namespace Arona.Datas.InMemory;

public class MemoryEmbeddingModel
{
    public required string Id { get; set; }
    public required string ModelId { get; set; }
    public required string Name { get; set; }
    public required Uri Endpoint { get; set; }
    public required string ApiKey { get; set; }
    public int Dimensions { get; set; }
    public bool IsFixedDimension { get; set; } = true;
}
