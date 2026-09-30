namespace Arona.Datas;

public class Config
{
    public required string MainDb { get; init; }
    public string? AdminPassword { get; init; }
    public int EmbeddingDimension { get; init; }
    public string? OnebotListenUri { get; init; }
    public string? OnebotAccessToken { get; init; }
}
